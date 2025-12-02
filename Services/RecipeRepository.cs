using CamCook.Models;
using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;
using System.Linq;
using CamCook.Services;
using System.Text.RegularExpressions;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;

namespace CamCook.Services
{
    public class RecipeRepository : IRecipeRepository
    {
        private readonly FirestoreDb _db;
        private readonly IImageStorage _imgStore;
        private const string Col = "recetas";

        private static readonly Regex _multiSpaces = new(@"\s{2,}", RegexOptions.Compiled);

        private static string CleanSpaces(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var s = input.Trim();
            s = _multiSpaces.Replace(s, " ");
            return s;
        }

        private static string CleanLabel(string? input)
        {
            var s = CleanSpaces(input);
            if (string.IsNullOrEmpty(s))
                return s;

            return char.ToUpper(s[0]) + (s.Length > 1 ? s.Substring(1) : string.Empty);
        }

        private const int MaxIngredientNameLength = 80;
        private static string CleanIngredientName(string? input)
        {
            var s = CleanLabel(input);
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length > MaxIngredientNameLength ? s.Substring(0, MaxIngredientNameLength) : s;
        }

        private const int MaxIngredientUnitLength = 20;
        private static string CleanIngredientUnit(string? input)
        {
            var s = CleanLabel(input);
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length > MaxIngredientUnitLength ? s.Substring(0, MaxIngredientUnitLength) : s;
        }

        private static string CleanQuantity(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var trimmed = input.Trim();
            var filteredChars = trimmed.Where(c =>
                char.IsDigit(c) || c == ' ' || c == '/' || c == '.' || c == ',');

            var filtered = new string(filteredChars.ToArray());
            filtered = _multiSpaces.Replace(filtered, " ");
            return filtered;
        }

        public RecipeRepository(FirestoreDb db, IImageStorage imgStore)
        {
            _db = db;
            _imgStore = imgStore;
        }

        // === CREATE ===
        public async Task<string> CreateAsync(RecipeInput input, CancellationToken ct = default)
        {
            try
            {
                string? mainUrl = null;

                // Subida segura de imagen principal
                if (input.MainImage != null && input.MainImage.Length > 0)
                {
                    try
                    {
                        mainUrl = await _imgStore.SaveAsync(input.MainImage, ct);
                    }
                    catch (Exception imgEx)
                    {
                        Console.WriteLine("ERROR al subir imagen principal: " + imgEx);
                        mainUrl = null; // seguimos sin romper la página
                    }
                }

                // Pasos
                var pasosEs = new List<Dictionary<string, object?>>();
                if (input.Steps != null)
                {
                    for (int i = 0; i < input.Steps.Count; i++)
                    {
                        var s = input.Steps[i];
                        string? stepUrl = s.ImageUrl;

                        if (s.Image != null && s.Image.Length > 0)
                        {
                            try
                            {
                                stepUrl = await _imgStore.SaveAsync(s.Image, ct);
                            }
                            catch (Exception stepImgEx)
                            {
                                Console.WriteLine($"ERROR al subir imagen del paso {i + 1}: {stepImgEx}");
                                stepUrl = null; // seguimos sin romper la página
                            }
                        }

                        pasosEs.Add(new Dictionary<string, object?>
                        {
                            ["orden"] = i,
                            ["descripcion"] = HtmlEncoder.Default.Encode(s.Description?.Trim() ?? string.Empty),
                            ["imagenUrl"] = stepUrl
                        });
                    }
                }

                // Ingredientes
                var ingredientesEs = input.Ingredients?.Select(i => new Dictionary<string, object?>
                {
                    ["nombre"] = HtmlEncoder.Default.Encode(i.Name ?? string.Empty),
                    ["cantidad"] = HtmlEncoder.Default.Encode(i.Quantity ?? string.Empty),
                    ["unidad"] = HtmlEncoder.Default.Encode(i.Unit ?? string.Empty)
                }).ToList();

                // ===================== IA: Analizar contenido =====================

                // Texto de ingredientes (solo nombres)
                var ingredientesTexto = string.Join(" ",
                    (input.Ingredients ?? new List<IngredientInput>())
                        .Select(i => i.Name ?? string.Empty));

                // Texto de pasos (descripciones)
                var pasosTexto = string.Join(" ",
                    (input.Steps ?? new List<StepInput>())
                        .Select(s => s.Description ?? string.Empty));

                // Texto combinado para la IA
                var descripcionCompuesta = $"{input.Title} {ingredientesTexto} {pasosTexto}".Trim();

                // ¿Tiene alguna imagen?
                bool tieneImagen =
                    (mainUrl != null) ||
                    (input.Steps?.Any(s => s.Image != null || !string.IsNullOrWhiteSpace(s.ImageUrl)) ?? false);

                // Llamamos al analizador IA
                var ai = AnalizadorIA.Analizar(
                    input.Title ?? string.Empty,
                    descripcionCompuesta,
                    input.Ingredients?.Count ?? 0,
                    tieneImagen
                );

                // Bloque que se guardará en "ai"
                var aiDict = new Dictionary<string, object?>
                {
                    ["score"] = ai.Score,
                    ["flags"] = new Dictionary<string, object?>
                    {
                        ["verdict"] = ai.Verdict.ToString().ToLowerInvariant(), // "ok", "review", "reject"
                        ["flags"] = ai.Flags,                                   // lista de mensajes
                        ["reason"] = string.Join("; ", ai.Flags)                // opcional: todo junto
                    }
                };

                // Estado según veredicto de la IA
                string estado = ai.Verdict switch
                {
                    AiVerdict.Ok => "pendiente_admin",   // pasa a moderación humana
                    AiVerdict.Review => "revisar",       // IA tiene dudas
                    AiVerdict.Reject => "rechazada_ai",  // IA la rechaza directamente
                    _ => "pendiente_admin"
                };

                // Descripción "plana" para la moderación (solo texto)
                var descripcionPlano = string.Join("\n",
                    pasosEs.Select(p => p["descripcion"]?.ToString() ?? string.Empty));

                var nowTs = Timestamp.GetCurrentTimestamp();

                var docData = new Dictionary<string, object?>
                {
                    ["titulo"] = HtmlEncoder.Default.Encode(input.Title?.Trim() ?? string.Empty),
                    ["calorias"] = input.Calories,
                    ["porciones"] = input.Servings,
                    ["tiempoPrep"] = input.PrepTimeText,
                    ["imagenUrl"] = mainUrl,

                    // Autor (doble: authorUid y autorUid para compatibilidad)
                    ["authorUid"] = input.AuthorUid,
                    ["authorName"] = input.AuthorName,
                    ["authorEmail"] = input.AuthorEmail,
                    ["autorUid"] = input.AuthorUid,
                    ["autorEmail"] = input.AuthorEmail,

                    // Contenido
                    ["ingredientes"] = ingredientesEs ?? new List<Dictionary<string, object?>>(),
                    ["pasos"] = pasosEs ?? new List<Dictionary<string, object?>>(),
                    ["descripcion"] = HtmlEncoder.Default.Encode(descripcionPlano),  // usado en la vista de moderación

                    // IA y estado
                    ["ai"] = aiDict,
                    ["estado"] = estado,
                    ["publicada"] = false,

                    // Timestamps
                    ["creadoEn"] = nowTs,
                    ["actualizadoEn"] = nowTs
                };

                // Guardar en Firestore
                var added = await _db.Collection(Col).AddAsync(docData, ct);
                return added.Id;
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR en CreateAsync: " + ex);
                throw new Exception("Ocurrió un error al crear la receta. Revisa los logs para más detalles.", ex);
            }
        }



        // === READ ALL ===
        public async Task<List<Recipe>> GetAllAsync(CancellationToken ct = default)
        {
            var snapshot = await _db.Collection(Col)
                .OrderByDescending("creadoEn")
                .GetSnapshotAsync(ct);

            return snapshot.Documents
                .Select(d =>
                {
                    var recipe = d.ConvertTo<Recipe>();
                    recipe.Id = d.Id;
                    return recipe;
                })
                .ToList();
        }

        // === READ ONE ===
        public async Task<Recipe?> GetByIdAsync(string id, CancellationToken ct = default)
        {
            var doc = await _db.Collection(Col).Document(id).GetSnapshotAsync(ct);
            if (!doc.Exists) return null;

            var recipe = doc.ConvertTo<Recipe>();
            recipe.Id = doc.Id;
            return recipe;
        }

        // === UPDATE ===
        public async Task UpdateAsync(string id, RecipeInput input, string currentUserUid, CancellationToken ct = default)
        {
            var docRef = _db.Collection(Col).Document(id);
            var doc = await docRef.GetSnapshotAsync(ct);
            if (!doc.Exists)
                throw new Exception("Receta no encontrada");

            // --- Resolver authorUid ---
            string? authorUid = null;

            if (doc.TryGetValue("authorUid", out string authorUidEn) && !string.IsNullOrWhiteSpace(authorUidEn))
                authorUid = authorUidEn;
            else if (doc.TryGetValue("autorUid", out string authorUidEs) && !string.IsNullOrWhiteSpace(authorUidEs))
                authorUid = authorUidEs;
            else if (doc.TryGetValue("authorEmail", out string _))
                authorUid = currentUserUid; // fallback para recetas viejas
            else
                throw new Exception("El documento no tiene authorUid/autorUid.");

            if (authorUid != currentUserUid)
                throw new UnauthorizedAccessException("No tienes permiso para editar esta receta");

            // --- Imagen principal ---
            string? mainUrl = null;
            try
            {
                if (input.MainImage != null && input.MainImage.Length > 0)
                    mainUrl = await _imgStore.SaveAsync(input.MainImage, ct);
                else
                {
                    if (doc.TryGetValue("imagenUrl", out string existingEs) && !string.IsNullOrWhiteSpace(existingEs))
                        mainUrl = existingEs;
                    else if (doc.TryGetValue("mainImageUrl", out string existingCamel) && !string.IsNullOrWhiteSpace(existingCamel))
                        mainUrl = existingCamel;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error al subir la imagen principal: " + ex.Message, ex);
            }

            // --- Pasos ---
            var pasosEs = new List<Dictionary<string, object?>>();
            if (input.Steps != null)
            {
                for (int i = 0; i < input.Steps.Count; i++)
                {
                    var s = input.Steps[i];
                    string? stepUrl = s.ImageUrl;

                    try
                    {
                        if (s.Image != null && s.Image.Length > 0)
                            stepUrl = await _imgStore.SaveAsync(s.Image, ct);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Error al subir imagen del paso {i + 1}: {ex.Message}", ex);
                    }

                    pasosEs.Add(new Dictionary<string, object?>
                    {
                        ["orden"] = s.Order,
                        ["descripcion"] = HtmlEncoder.Default.Encode(s.Description?.Trim() ?? string.Empty),
                        ["imagenUrl"] = stepUrl
                    });
                }
            }

            // --- Ingredientes ---
            var ingredientesEs = input.Ingredients?.Select(i => new Dictionary<string, object?>
            {
                ["nombre"] = HtmlEncoder.Default.Encode(i.Name ?? string.Empty),
                ["cantidad"] = HtmlEncoder.Default.Encode(i.Quantity ?? string.Empty),
                ["unidad"] = HtmlEncoder.Default.Encode(i.Unit ?? string.Empty)
            }).ToList();

            var nowTs = Timestamp.FromDateTime(DateTime.UtcNow);

            var updateData = new Dictionary<string, object?>
            {
                ["titulo"] = HtmlEncoder.Default.Encode(input.Title ?? string.Empty),
                ["calorias"] = input.Calories,
                ["porciones"] = input.Servings,
                ["tiempoPrep"] = input.PrepTimeText,
                ["imagenUrl"] = mainUrl,
                ["ingredientes"] = ingredientesEs ?? new(),
                ["pasos"] = pasosEs ?? new(),
                ["estado"] = "pendiente_admin",
                ["publicada"] = false,
                ["publicadoEn"] = FieldValue.Delete,
                ["actualizadoEn"] = nowTs,
                ["ai"] = new Dictionary<string, object?>
                {
                    ["status"] = "pending",
                    ["requestedAt"] = nowTs,
                    ["version"] = FieldValue.Increment(1),
                    ["flags"] = new Dictionary<string, object?>()
                },
                ["needsModeration"] = true
            };

            await docRef.UpdateAsync(updateData, cancellationToken: ct);

            // --- Limpieza de autorUid vieja (segura) ---
            try
            {
                if (doc.ContainsField("autorUid"))
                {
                    await docRef.UpdateAsync(new Dictionary<string, object?>
                    {
                        ["authorUid"] = authorUid,
                        ["autorUid"] = FieldValue.Delete
                    }, cancellationToken: ct);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Advertencia: no se pudo limpiar autorUid. " + ex.Message);
            }
        }

        // === DELETE ===
        public async Task DeleteAsync(string id, string currentUserUid, CancellationToken ct = default)
        {
            var docRef = _db.Collection(Col).Document(id);
            var doc = await docRef.GetSnapshotAsync(ct);

            if (!doc.Exists)
                throw new Exception("Receta no encontrada");

            var authorUid = doc.GetValue<string>("authorUid");
            if (authorUid != currentUserUid)
                throw new UnauthorizedAccessException("No tienes permiso para eliminar esta receta");

            await docRef.DeleteAsync(cancellationToken: ct);
        }

        // === CREATE DRAFT (BORRADOR) ===
        public async Task<string> CreateDraftAsync(RecipeInput input, CancellationToken ct = default)
        {
            // 1) Crear receta con toda la lógica normal (imágenes, IA, etc.)
            var id = await CreateAsync(input, ct);

            // 2) Forzar a borrador y que NO esté publicada ni en marketplace
            var docRef = _db.Collection(Col).Document(id);

            await docRef.UpdateAsync(new Dictionary<string, object?>
            {
                ["estado"] = "borrador",
                ["publicada"] = false,
                ["enMarketplace"] = false,
                ["publicadoEn"] = FieldValue.Delete
            }, cancellationToken: ct);

            return id;
        }

        public async Task UpdateDraftAsync(string id, RecipeInput input, string currentUserUid, CancellationToken ct = default)
        {
            // Actualizas normalmente el contenido de la receta
            await UpdateAsync(id, input, currentUserUid, ct);

            // Y luego vuelves a marcarla como borrador
            var docRef = _db.Collection(Col).Document(id);

            await docRef.UpdateAsync(new Dictionary<string, object?>
            {
                ["estado"] = "borrador",
                ["publicada"] = false,
                ["enMarketplace"] = false,
                ["publicadoEn"] = FieldValue.Delete
            }, cancellationToken: ct);
        }


    }
}
