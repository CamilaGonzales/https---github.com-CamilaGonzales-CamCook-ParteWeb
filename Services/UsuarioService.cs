using Google.Cloud.Firestore;

namespace CamCook.Services;

public class UsuarioService
{
    private readonly FirestoreDb _db;
    public UsuarioService(FirestoreDb db) => _db = db;

    public async Task<Dictionary<string, List<string>>> ObtenerRolesAsync(IEnumerable<string> uids)
    {
        var dict = new Dictionary<string, List<string>>();
        var ids = uids.Distinct().ToList();
        if (ids.Count == 0) return dict;

        // Firestore limita a 10 por WhereIn; si hay más, paginamos manualmente.
        const int batch = 10;
        for (int i = 0; i < ids.Count; i += batch)
        {
            var slice = ids.Skip(i).Take(batch).ToList();
            var snap = await _db.Collection("usuarios")
                .WhereIn(FieldPath.DocumentId, slice)
                .GetSnapshotAsync();

            foreach (var doc in snap.Documents)
            {
                var roles = new List<string>();
                var data = doc.ToDictionary();

                if (data.TryGetValue("roles", out var arr) && arr is IEnumerable<object> arrObj)
                {
                    roles = arrObj.Select(o => o?.ToString() ?? "")
                                  .Where(s => !string.IsNullOrWhiteSpace(s))
                                  .Distinct(StringComparer.OrdinalIgnoreCase)
                                  .ToList();
                }

                // Compatibilidad con campo legado "rol"
                if (roles.Count == 0 && data.TryGetValue("rol", out var r0))
                {
                    var r = r0?.ToString();
                    if (!string.IsNullOrWhiteSpace(r)) roles.Add(r!);
                }

                if (roles.Count == 0) roles.Add("usuario"); // default
                dict[doc.Id] = roles;
            }
        }

        return dict;
    }
}
