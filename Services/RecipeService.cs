using Google.Cloud.Firestore;

namespace CamCook.Services;

public class RecetaService
{
    private readonly FirestoreDb _db;
    public RecetaService(FirestoreDb db) => _db = db;

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

    /// <summary>
    /// Publica la receta y agrega el rol "chef" al autor (roles como array).
    /// Conserva el campo legado "rol" si ya lo usas en otras partes.
    /// </summary>
    public async Task AprobarRecetaAsync(string recetaId, string? autorUid = null)
    {
        // 1) Publicar receta
        var recetaRef = _db.Collection("recetas").Document(recetaId);
        await recetaRef.UpdateAsync(new Dictionary<string, object>
        {
            ["estado"] = "publicada",
            ["publicadoEn"] = Timestamp.FromDateTime(DateTime.UtcNow)
        });

        // 2) Agregar rol "chef" al autor (array de roles)
        if (!string.IsNullOrWhiteSpace(autorUid))
        {
            var userRef = _db.Collection("usuarios").Document(autorUid);

            // Asegura el array de roles y agrega "chef" sin duplicar
            await userRef.UpdateAsync(new Dictionary<string, object>
            {
                ["roles"] = FieldValue.ArrayUnion("usuario", "chef"), // si no existía "usuario", también lo agrega
                ["actualizadoEn"] = Timestamp.FromDateTime(DateTime.UtcNow)
            });

            // (Opcional) Mantener compatibilidad con campo legado "rol"
            // - Si dependes de 'rol' en algún lado, puedes setearlo a "chef"
            //   solo si antes era "usuario". Si no lo necesitas, elimina este bloque.
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
            ["motivoRechazo"] = motivo // <-- NUEVO: campo plano para leer fácil en UI
        };

        // Mantener también en ai.flags.reasonAdmin (o reason si prefieres)
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

            flags["reasonAdmin"] = motivo; // diferenciamos del motivo de la IA
            ai["flags"] = flags;
            updates["ai"] = ai;
        }

        await docRef.UpdateAsync(updates);
    }

    /// <summary>
    /// Devuelve recetas publicadas, ordenadas por publicadoEn (si existe).
    /// </summary>
    public async Task<List<Dictionary<string, object>>> ObtenerPublicadasAsync(int limit = 100)
    {
        // Nada de OrderBy (evita el índice compuesto)
        var query = _db.Collection("recetas")
                       .WhereEqualTo("estado", "publicada")
                       .Limit(limit);

        var snap = await query.GetSnapshotAsync();

        var list = new List<Dictionary<string, object>>();

        foreach (var doc in snap.Documents)
        {
            var dict = doc.ToDictionary();
            dict["id"] = doc.Id;

            // --- OPCIONAL: agregar un campo temporal para ordenar en memoria ---
            // 1) Si tienes 'publicadoEn' como Timestamp en el doc, úsalo:
            if (dict.TryGetValue("publicadoEn", out var pubObj) && pubObj is Timestamp tsPub)
            {
                dict["__orden"] = tsPub.ToDateTime();
            }
            // 2) Si NO tienes 'publicadoEn', usa la hora de creación del snapshot:
            else if (doc.CreateTime.HasValue)
            {
                dict["__orden"] = doc.CreateTime.Value.ToDateTime();
            }
            else
            {
                dict["__orden"] = DateTime.MinValue;
            }
            // -------------------------------------------------------------------
            list.Add(dict);
        }

        // Ordenar en memoria (no requiere índice)
        list = list
            .OrderByDescending(d => d["__orden"])
            .ToList();

        // limpiar el campo temporal si quieres
        foreach (var d in list) d.Remove("__orden");

        return list;
    }
}
