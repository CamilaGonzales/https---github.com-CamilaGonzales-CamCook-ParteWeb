using CamCook.Models;
using Google.Cloud.Firestore;

public class BuscadorService
{
    private readonly FirestoreDb _db;

    public BuscadorService(FirestoreDb db)
    {
        _db = db;
    }

    public async Task<List<RecipieViewModel>> BuscarAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<RecipieViewModel>();

        // Buscar recetas publicadas que contengan el query en título o descripción
        var snap = await _db.Collection("recetas")
            .WhereEqualTo("estado", "publicada")
            .GetSnapshotAsync();

        var results = new List<RecipieViewModel>();

        foreach (var doc in snap.Documents)
        {
            var dict = doc.ToDictionary();
            dict.TryGetValue("titulo", out var tituloObj);
            dict.TryGetValue("descripcion", out var descObj);

            string titulo = tituloObj?.ToString() ?? "";
            string descripcion = descObj?.ToString() ?? "";

            // Filtrado simple: contener el texto (igual que Android)
            if (!titulo.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                !descripcion.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Obtener imagen principal
            string imagenUrl = dict.TryGetValue("imagenUrl", out var imgObj) && !string.IsNullOrWhiteSpace(imgObj?.ToString())
                ? imgObj.ToString()!
                : ""; // fallback

            // 🔹 Fallback: primera imagen de los pasos
            if (string.IsNullOrWhiteSpace(imagenUrl) && dict.TryGetValue("pasos", out var pasosObj) && pasosObj is IEnumerable<object> pasos)
            {
                foreach (var paso in pasos)
                {
                    if (paso is IDictionary<string, object> pasoDict &&
                        pasoDict.TryGetValue("imagenUrl", out var imgPasoObj) &&
                        !string.IsNullOrWhiteSpace(imgPasoObj?.ToString()))
                    {
                        imagenUrl = imgPasoObj!.ToString();
                        break;
                    }
                }
            }

            results.Add(new RecipieViewModel
            {
                Id = doc.Id,
                Titulo = titulo,
                Descripcion = descripcion,
                ImagenUrl = imagenUrl,
                Autor = dict.TryGetValue("autorUid", out var autorObj) ? autorObj?.ToString() : "",
                Publicado = true
            });
        }

        return results;
    }
    public async Task<List<string>> BuscarIdsExactosComoBuscadorAsync(string texto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return new List<string>();

        var snap = await _db.Collection("recetas")
            .WhereEqualTo("estado", "publicada")
            .GetSnapshotAsync(ct);

        var s = texto.Trim().ToLower();
        var list = new List<Dictionary<string, object>>();

        foreach (var doc in snap.Documents)
        {
            var dict = doc.ToDictionary();
            dict["id"] = doc.Id;

            var titulo = dict.TryGetValue("titulo", out var t) ? t?.ToString()?.ToLower() : "";
            var desc = dict.TryGetValue("descripcion", out var d) ? d?.ToString()?.ToLower() : "";

            if ((titulo != null && titulo.Contains(s)) ||
                (desc != null && desc.Contains(s)))
            {
                list.Add(dict);
            }
        }

        // 👉 1) mismo orden que BuscadorService (por coincidencia)
        list = list
            .OrderByDescending(r =>
            {
                var titulo = r.TryGetValue("titulo", out var t) ? t?.ToString()?.ToLower() : "";
                return titulo != null && titulo.Contains(s) ? 1 : 0;
            })
            .ToList();

        // 👉 2) no incluir sopa de chairo ni recetas que NO estén en el resultado filtrado.
        return list.Select(r => r["id"].ToString()!).ToList();
    }

}
