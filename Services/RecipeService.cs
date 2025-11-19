using CamCook.Models;
using Google.Cloud.Firestore;
using System.Linq;

namespace CamCook.Services;

public class RecetaService
{
    private readonly FirestoreDb _db;
    public RecetaService(FirestoreDb db) => _db = db;

    // 🔹 NUEVO: crear receta usando RecipeInput + IA
    public async Task<string> CrearRecetaAsync(RecipeInput input)
    {
        // 1) Normalizar listas
        var ingredientesInput = input.Ingredients ?? new List<IngredientInput>();
        var pasosInput = input.Steps ?? new List<StepInput>();

        // 2) Texto para la IA: título + nombres de ingredientes + descripciones de pasos
        var ingredientesTexto = string.Join(" ",
            ingredientesInput.Select(i => i.Name ?? string.Empty));

        var pasosTexto = string.Join(" ",
            pasosInput.Select(s => s.Description ?? string.Empty));

        var descripcionCompuesta = $"{input.Title} {ingredientesTexto} {pasosTexto}".Trim();

        bool tieneImagen =
            (input.MainImage != null) ||
            pasosInput.Any(s => !string.IsNullOrWhiteSpace(s.ImageUrl));

        // 3) Llamar a la IA
        var ai = AnalizadorIA.Analizar(
            input.Title ?? string.Empty,
            descripcionCompuesta,
            ingredientesInput.Count,
            tieneImagen
        );

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

        // 4) Estado inicial según veredicto
        string estado = ai.Verdict switch
        {
            AiVerdict.Ok => "pendiente_admin",
            AiVerdict.Review => "revisar",
            AiVerdict.Reject => "rechazada_ai",
            _ => "pendiente_admin"
        };

        // 5) Mapear ingredientes a lo que espera Firestore (Recipe/Ingredient)
        var ingredientesFs = ingredientesInput
            .Where(i => !string.IsNullOrWhiteSpace(i.Name))
            .Select(i => new Dictionary<string, object?>
            {
                ["nombre"] = i.Name ?? string.Empty,
                ["cantidad"] = i.Quantity ?? string.Empty,
                ["unidad"] = i.Unit ?? string.Empty
            })
            .ToList();

        // 6) Mapear pasos a lo que espera Firestore (CookStep)
        var pasosFs = pasosInput
            .Where(s => !string.IsNullOrWhiteSpace(s.Description))
            .Select((s, idx) => new Dictionary<string, object?>
            {
                ["descripcion"] = s.Description ?? string.Empty,
                ["imagenUrl"] = s.ImageUrl ?? string.Empty,
                ["orden"] = s.Order != 0 ? s.Order : idx + 1
            })
            .ToList();

        // 7) Descripción "plana" para usar en moderación (solo texto)
        var descripcionPlano = string.Join("\n",
            pasosFs.Select(p => p["descripcion"]?.ToString() ?? string.Empty));

        // 8) Autor
        var authorUid = input.AuthorUid ?? string.Empty;
        var authorEmail = input.AuthorEmail ?? string.Empty;

        // 9) Imagen principal: si ya tienes una pipeline distinta, cambia esta línea
        var imagenPrincipalUrl = pasosFs.FirstOrDefault()?["imagenUrl"]?.ToString() ?? string.Empty;

        // 10) Armar diccionario final para Firestore
        var data = new Dictionary<string, object?>
        {
            ["titulo"] = input.Title ?? string.Empty,
            ["calorias"] = input.Calories ?? 0,
            ["porciones"] = input.Servings ?? 0,
            ["tiempoPrep"] = input.PrepTimeText ?? string.Empty,
            ["imagenUrl"] = imagenPrincipalUrl,          // 🔸 aquí lee la moderación
            ["creadoEn"] = Timestamp.FromDateTime(DateTime.UtcNow),

            ["descripcion"] = descripcionPlano,          // 🔸 la moderación usa este campo
            ["ingredientes"] = ingredientesFs,
            ["pasos"] = pasosFs,

            // info de autor (doble por compatibilidad)
            ["authorUid"] = authorUid,
            ["authorEmail"] = authorEmail,
            ["autorUid"] = authorUid,
            ["autorEmail"] = authorEmail,

            ["estado"] = estado,
            ["ai"] = aiDict
        };

        // 11) Guardar en la colección "recetas"
        var docRef = _db.Collection("recetas").Document(); // id automático
        await docRef.SetAsync(data);

        return docRef.Id;
    }

    // 🔹 Lo demás queda como tú lo tenías:

    public async Task<List<RecetaPendienteDto>> ObtenerPendientesAsync()
    {
        var estados = new[] { "pendiente_admin", "revisar", "rechazada_ai", "rechazada" };
        var snap = await _db.Collection("recetas").WhereIn("estado", estados).GetSnapshotAsync();

        var list = new List<RecetaPendienteDto>();
        foreach (var doc in snap.Documents)
        {
            var dict = doc.ToDictionary();
            dict.TryGetValue("estado", out var st);
            list.Add(new RecetaPendienteDto
            {
                Id = doc.Id,
                Data = dict,
                Estado = st?.ToString()
            });
        }
        return list;
    }

    public async Task AprobarRecetaAsync(string recetaId, string? autorUid = null)
    {
        var recetaRef = _db.Collection("recetas").Document(recetaId);
        await recetaRef.UpdateAsync(new Dictionary<string, object>
        {
            ["estado"] = "publicada",
            ["publicadoEn"] = Timestamp.FromDateTime(DateTime.UtcNow)
        });

        if (!string.IsNullOrWhiteSpace(autorUid))
        {
            var userRef = _db.Collection("usuarios").Document(autorUid);

            await userRef.UpdateAsync(new Dictionary<string, object>
            {
                ["roles"] = FieldValue.ArrayUnion("usuario", "chef"),
                ["actualizadoEn"] = Timestamp.FromDateTime(DateTime.UtcNow)
            });

            await _db.RunTransactionAsync(async tx =>
            {
                var snap = await tx.GetSnapshotAsync(userRef);
                if (!snap.Exists) return;

                var rolAntiguo = snap.ContainsField("rol") ? snap.GetValue<string>("rol") : "";
                if (string.IsNullOrWhiteSpace(rolAntiguo) || rolAntiguo == "usuario")
                {
                    tx.Update(userRef, new Dictionary<string, object> { ["rol"] = "chef" });
                }
            });
        }
    }

    public async Task RechazarRecetaAsync(string id, string motivo)
    {
        var docRef = _db.Collection("recetas").Document(id);

        var updates = new Dictionary<string, object>
        {
            ["estado"] = "rechazada",
            ["rechazadaEn"] = Timestamp.FromDateTime(DateTime.UtcNow),
            ["motivoRechazo"] = motivo
        };

        var snap = await docRef.GetSnapshotAsync();
        if (snap.Exists)
        {
            var dict = snap.ToDictionary();

            var ai = dict.TryGetValue("ai", out var aiObj) && aiObj is Dictionary<string, object> aiDict
                ? aiDict
                : new Dictionary<string, object>();

            var flags = ai.TryGetValue("flags", out var flagsObj) && flagsObj is Dictionary<string, object> flagsDict
                ? flagsDict
                : new Dictionary<string, object>();

            flags["reasonAdmin"] = motivo;
            ai["flags"] = flags;
            updates["ai"] = ai;
        }

        await docRef.UpdateAsync(updates);
    }

    public async Task<List<Dictionary<string, object>>> ObtenerPublicadasAsync(int limit = 100)
    {
        var query = _db.Collection("recetas")
                       .WhereEqualTo("estado", "publicada")
                       .Limit(limit);

        var snap = await query.GetSnapshotAsync();

        var list = new List<Dictionary<string, object>>();

        foreach (var doc in snap.Documents)
        {
            var dict = doc.ToDictionary();
            dict["id"] = doc.Id;

            if (dict.TryGetValue("publicadoEn", out var pubObj) && pubObj is Timestamp tsPub)
            {
                dict["__orden"] = tsPub.ToDateTime();
            }
            else if (doc.CreateTime.HasValue)
            {
                dict["__orden"] = doc.CreateTime.Value.ToDateTime();
            }
            else
            {
                dict["__orden"] = DateTime.MinValue;
            }

            list.Add(dict);
        }

        list = list
            .OrderByDescending(d => d["__orden"])
            .ToList();

        foreach (var d in list) d.Remove("__orden");

        return list;
    }

    public async Task EliminarRecetaAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return;

        var docRef = _db.Collection("recetas").Document(id);
        await docRef.DeleteAsync();
    }


    public async Task<IDictionary<string, object>?> ObtenerPorIdAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        var docRef = _db.Collection("recetas").Document(id);
        var snap = await docRef.GetSnapshotAsync(ct);

        if (!snap.Exists) return null;

        return snap.ToDictionary();
    }
}
