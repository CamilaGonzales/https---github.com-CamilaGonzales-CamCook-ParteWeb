using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CamCook.Models;
using Google.Cloud.Firestore;

namespace CamCook.Services
{
    public class RecipeRepository : IRecipeRepository
    {
        private readonly FirestoreDb _db;
        private readonly IImageStorage _imgStore;
        private const string Col = "recetas";

        public RecipeRepository(FirestoreDb db, IImageStorage imgStore)
        {
            _db = db;
            _imgStore = imgStore;
        }

        /// <summary>
        /// Crea una receta a partir del formulario y devuelve el Id del documento en Firestore.
        /// Guarda claves en español y camelCase para compatibilidad.
        /// </summary>
        public async Task<string> CreateAsync(RecipeInput input, CancellationToken ct = default)
        {
            // 1) Subir imagen principal
            string? mainUrl = null;
            if (input.MainImage != null && input.MainImage.Length > 0)
                mainUrl = await _imgStore.SaveAsync(input.MainImage, ct);

            // 2) Subir imágenes de pasos
            var stepsCamel = new List<Dictionary<string, object?>>();
            var pasosEs = new List<Dictionary<string, object?>>();

            if (input.Steps != null)
            {
                for (int i = 0; i < input.Steps.Count; i++)
                {
                    var s = input.Steps[i];
                    string? stepUrl = null;

                    if (s.Image != null && s.Image.Length > 0)
                        stepUrl = await _imgStore.SaveAsync(s.Image, ct);

                    // camelCase
                    stepsCamel.Add(new Dictionary<string, object?>
                    {
                        ["index"] = i, // 0..N-1
                        ["description"] = s.Description?.Trim(),
                        ["imageUrl"] = stepUrl
                    });

                    // español
                    pasosEs.Add(new Dictionary<string, object?>
                    {
                        ["orden"] = i, // si prefieres 1..N, usa i+1
                        ["descripcion"] = s.Description?.Trim(),
                        ["imagenUrl"] = stepUrl
                    });
                }
            }

            // 3) Ingredientes (dos variantes)
            var ingredientsCamel = input.Ingredients?.Select(i => new Dictionary<string, object?>
            {
                ["name"] = i.Name,
                ["quantity"] = i.Quantity,
                ["unit"] = i.Unit
            }).ToList();

            var ingredientesEs = input.Ingredients?.Select(i => new Dictionary<string, object?>
            {
                ["nombre"] = i.Name,
                ["cantidad"] = i.Quantity,
                ["unidad"] = i.Unit
            }).ToList();

            var title = input.Title?.Trim();
            var nowTs = Timestamp.GetCurrentTimestamp();

            // 4) Documento (ES + camelCase)
            var docData = new Dictionary<string, object?>
            {
                // Título
                ["title"] = title,
                ["titulo"] = title,

                // Calorías / Porciones
                ["calories"] = input.Calories,
                ["calorias"] = input.Calories,
                ["servings"] = input.Servings,
                ["porciones"] = input.Servings,

                // Tiempo de preparación
                ["prepTimeText"] = input.PrepTimeText,
                ["tiempoPrep"] = input.PrepTimeText,

                // Imagen principal
                ["mainImageUrl"] = mainUrl,
                ["imagenUrl"] = mainUrl,

                // Ingredientes / Pasos
                ["ingredients"] = ingredientsCamel,
                ["ingredientes"] = ingredientesEs,
                ["steps"] = stepsCamel,
                ["pasos"] = pasosEs,

                // Autoría
                ["authorUid"] = input.AuthorUid,
                ["authorEmail"] = input.AuthorEmail,

                // Estado / publicación
                ["estado"] = "pendiente_admin",
                ["publicada"] = false,

                // Tiempos
                ["creadoEn"] = nowTs,
                ["actualizadoEn"] = nowTs
            };

            var added = await _db.Collection(Col).AddAsync(docData, ct);
            return added.Id;
        }

        // READ ALL
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

        // READ ONE
        public async Task<Recipe?> GetByIdAsync(string id, CancellationToken ct = default)
        {
            var doc = await _db.Collection(Col).Document(id).GetSnapshotAsync(ct);
            if (!doc.Exists) return null;

            var recipe = doc.ConvertTo<Recipe>();
            recipe.Id = doc.Id;
            return recipe;
        }

        // UPDATE
        public async Task UpdateAsync(string id, RecipeInput input, string currentUserUid, CancellationToken ct = default)
        {
            var docRef = _db.Collection(Col).Document(id);
            var doc = await docRef.GetSnapshotAsync(ct);
            if (!doc.Exists) throw new Exception("Receta no encontrada");

            var authorUid = doc.GetValue<string>("authorUid");
            if (authorUid != currentUserUid)
                throw new UnauthorizedAccessException("No tienes permiso para editar esta receta");

            // MAIN IMAGE: si hay nueva, subir; si no, conservar la existente
            string? mainUrl = null;
            if (input.MainImage != null && input.MainImage.Length > 0)
            {
                mainUrl = await _imgStore.SaveAsync(input.MainImage, ct);
            }
            else
            {
                // conservar actual del documento
                if (doc.TryGetValue("imagenUrl", out string existingEs) && !string.IsNullOrWhiteSpace(existingEs))
                    mainUrl = existingEs;
                else if (doc.TryGetValue("mainImageUrl", out string existingCamel) && !string.IsNullOrWhiteSpace(existingCamel))
                    mainUrl = existingCamel;
            }

            // PASOS: si sube nueva imagen, reemplaza; si no, conserva input.ImageUrl
            var stepsCamel = new List<Dictionary<string, object?>>();
            var pasosEs = new List<Dictionary<string, object?>>();

            if (input.Steps != null)
            {
                for (int i = 0; i < input.Steps.Count; i++)
                {
                    var s = input.Steps[i];
                    string? stepUrl = s.ImageUrl; // conservar por defecto

                    if (s.Image != null && s.Image.Length > 0)
                        stepUrl = await _imgStore.SaveAsync(s.Image, ct);

                    var order = s.Order; // viene del formulario (o 0..N-1)

                    // camel
                    stepsCamel.Add(new Dictionary<string, object?>
                    {
                        ["index"] = order,
                        ["description"] = s.Description?.Trim(),
                        ["imageUrl"] = stepUrl
                    });

                    // español
                    pasosEs.Add(new Dictionary<string, object?>
                    {
                        ["orden"] = order,
                        ["descripcion"] = s.Description?.Trim(),
                        ["imagenUrl"] = stepUrl
                    });
                }
            }

            // INGREDIENTES (dos variantes)
            var ingredientsCamel = input.Ingredients?.Select(i => new Dictionary<string, object?>
            {
                ["name"] = i.Name,
                ["quantity"] = i.Quantity,
                ["unit"] = i.Unit
            }).ToList();

            var ingredientesEs = input.Ingredients?.Select(i => new Dictionary<string, object?>
            {
                ["nombre"] = i.Name,
                ["cantidad"] = i.Quantity,
                ["unidad"] = i.Unit
            }).ToList();

            var updateData = new Dictionary<string, object?>
            {
                ["title"] = input.Title,
                ["titulo"] = input.Title,

                ["calories"] = input.Calories,
                ["calorias"] = input.Calories,
                ["servings"] = input.Servings,
                ["porciones"] = input.Servings,

                ["prepTimeText"] = input.PrepTimeText,
                ["tiempoPrep"] = input.PrepTimeText,

                ["mainImageUrl"] = mainUrl,
                ["imagenUrl"] = mainUrl,

                ["ingredients"] = ingredientsCamel,
                ["ingredientes"] = ingredientesEs,
                ["steps"] = stepsCamel,
                ["pasos"] = pasosEs,

                // RE-ENVÍO A REVISIÓN
                ["estado"] = "revisar",
                ["publicada"] = false,
                ["actualizadoEn"] = Timestamp.GetCurrentTimestamp(),
                ["publicadoEn"] = FieldValue.Delete // elimina fecha de publicación si existía
            };

            await docRef.UpdateAsync(updateData, cancellationToken: ct);
        }



        // DELETE
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


    }
}
