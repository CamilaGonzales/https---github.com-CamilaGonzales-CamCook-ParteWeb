using CamCook.Models;
using CamCook.Models.Api;
using Google.Cloud.Firestore;
using System;
using System.Linq;

namespace CamCook.Services;

public class RecetaService
{
    private readonly FirestoreDb _db;
    private const string Col = "recetas";
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

    //Lo demás queda como tú lo tenías:

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

    public async Task<List<Dictionary<string, object>>> ObtenerPublicadasAsync(string? search = null, int limit = 100)
    {
        // 👉 SOLO recetas publicadas (como antes)
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
        //  FILTRO DE BÚSQUEDA
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            list = list
                .Where(r =>
                    (r.TryGetValue("titulo", out var t) && t?.ToString()?.ToLower().Contains(s) == true) ||
                    (r.TryGetValue("descripcion", out var d) && d?.ToString()?.ToLower().Contains(s) == true)
                )
                .ToList();
        }
        list = list
            .OrderByDescending(d => d["__orden"])
            .ToList();

        foreach (var d in list) d.Remove("__orden");

        return list;
    }

    // 👉 NUEVO: publicadas + borradores del usuario
    public async Task<List<Dictionary<string, object>>> ObtenerPublicadasYBorradoresAsync(string? uid, string? search = null, int limitPublicadas = 100)
    {
        var list = new List<Dictionary<string, object>>();

        // 1) Publicadas (para todos)
        var queryPublicadas = _db.Collection("recetas")
                                 .WhereEqualTo("estado", "publicada")
                                 .Limit(limitPublicadas);

        var snapPub = await queryPublicadas.GetSnapshotAsync();

        foreach (var doc in snapPub.Documents)
        {
            var dict = doc.ToDictionary();
            dict["id"] = doc.Id;

            if (dict.TryGetValue("publicadoEn", out var pubObj) && pubObj is Timestamp tsPub)
                dict["__orden"] = tsPub.ToDateTime();
            else if (doc.CreateTime.HasValue)
                dict["__orden"] = doc.CreateTime.Value.ToDateTime();
            else
                dict["__orden"] = DateTime.MinValue;

            list.Add(dict);
        }

        // 2) Borradores SOLO del usuario actual
        if (!string.IsNullOrWhiteSpace(uid))
        {
            var queryBorradores = _db.Collection("recetas")
                                     .WhereEqualTo("estado", "borrador")
                                     .WhereEqualTo("authorUid", uid);

            var snapBor = await queryBorradores.GetSnapshotAsync();

            foreach (var doc in snapBor.Documents)
            {
                var dict = doc.ToDictionary();
                dict["id"] = doc.Id;

                if (dict.TryGetValue("creadoEn", out var creObj) && creObj is Timestamp tsCre)
                    dict["__orden"] = tsCre.ToDateTime();
                else
                    dict["__orden"] = DateTime.MinValue;

                list.Add(dict);
            }
        }
        // FILTRO DE BÚSQUEDA
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            list = list
                .Where(r =>
                    (r.TryGetValue("titulo", out var t) && t?.ToString()?.ToLower().Contains(s) == true) ||
                    (r.TryGetValue("descripcion", out var d) && d?.ToString()?.ToLower().Contains(s) == true)
                )
                .ToList();
        }
        // 3) Orden combinado
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
    // ✅ Versión con UID actual (NUEVA)
    public RecipieViewModel MapToVm(IDictionary<string, object> dict, string? currentUid)
    {
        if (dict == null) return new RecipieViewModel();

        dict.TryGetValue("id", out var idObj);
        dict.TryGetValue("titulo", out var tituloObj);
        dict.TryGetValue("descripcion", out var descripcionObj);
        dict.TryGetValue("imagenUrl", out var imagenObj);
        dict.TryGetValue("autorUid", out var autorUidObj);
        if (autorUidObj == null || string.IsNullOrWhiteSpace(autorUidObj.ToString()))
            dict.TryGetValue("authorUid", out autorUidObj);
        dict.TryGetValue("autorEmail", out var autorEmailObj);
        if (autorEmailObj == null || string.IsNullOrWhiteSpace(autorEmailObj.ToString()))
            dict.TryGetValue("authorEmail", out autorEmailObj);

        dict.TryGetValue("estado", out var estadoObj);
        dict.TryGetValue("pasos", out var pasosObj);
        dict.TryGetValue("calorias", out var caloriasObj);
        dict.TryGetValue("porciones", out var porcionesObj);
        dict.TryGetValue("tiempoPrep", out var tiempoPrepObj);
        dict.TryGetValue("likes", out var likesObj);
        dict.TryGetValue("views", out var viewsObj);

        string? id = idObj?.ToString();
        string? titulo = tituloObj?.ToString();
        string? descripcion = descripcionObj?.ToString();
        string? imagenUrl = imagenObj?.ToString();
        string? autorUid = autorUidObj?.ToString();
        string? autorEmail = autorEmailObj?.ToString();
        string? estado = estadoObj?.ToString();

        // Parsear números
        int? calorias = null;
        if (caloriasObj != null)
        {
            if (caloriasObj is int cInt) calorias = cInt;
            else if (caloriasObj is long cLong) calorias = (int)cLong;
            else if (int.TryParse(caloriasObj.ToString(), out var cParse)) calorias = cParse;
        }

        int? porciones = null;
        if (porcionesObj != null)
        {
            if (porcionesObj is int pInt) porciones = pInt;
            else if (porcionesObj is long pLong) porciones = (int)pLong;
            else if (int.TryParse(porcionesObj.ToString(), out var pParse)) porciones = pParse;
        }

        int? likes = null;
        if (likesObj != null)
        {
            if (likesObj is int lInt) likes = lInt;
            else if (likesObj is long lLong) likes = (int)lLong;
            else if (int.TryParse(likesObj.ToString(), out var lParse)) likes = lParse;
        }

        int? views = null;
        if (viewsObj != null)
        {
            if (viewsObj is int vInt) views = vInt;
            else if (viewsObj is long vLong) views = (int)vLong;
            else if (int.TryParse(viewsObj.ToString(), out var vParse)) views = vParse;
        }

        string? tiempoPrep = tiempoPrepObj?.ToString();

        // fallback: primera imagen de los pasos
        if (string.IsNullOrWhiteSpace(imagenUrl) && pasosObj is IEnumerable<object> pasos)
        {
            foreach (var p in pasos)
            {
                if (p is IDictionary<string, object> pasoDict &&
                    pasoDict.TryGetValue("imagenUrl", out var imgPasoObj))
                {
                    var imgPaso = imgPasoObj?.ToString();
                    if (!string.IsNullOrWhiteSpace(imgPaso))
                    {
                        imagenUrl = imgPaso;
                        break;
                    }
                }
            }
        }

        var vm = new RecipieViewModel
        {
            Id = id,
            Titulo = titulo,
            Descripcion = descripcion,
            ImagenUrl = imagenUrl,
            Autor = autorEmail,       // Nombre o email del autor
            AutorUid = autorUid,      // Muy importante: para la condición en la vista
            Publicado = estado == "publicada",
            Calorias = calorias,
            Porciones = porciones,
            Likes = likes,
            Views = views,
            TiempoPrep = tiempoPrep
        };

        // AQUÍ está lo que me pediste, adaptado al dict
        if (!string.IsNullOrWhiteSpace(currentUid) &&
            !string.IsNullOrWhiteSpace(autorUid))
        {
            vm.EsAutor = autorUid == currentUid;
        }

        return vm;
    }

    // Versión vieja que sigue funcionando igual (NO rompe nada)
    public RecipieViewModel MapToVm(IDictionary<string, object> dict)
        => MapToVm(dict, null);



    public async Task<Recipe?> ObtenerPorIdAsync(string id)
    {
        var snap = await _db.Collection("recetas").Document(id).GetSnapshotAsync();
        if (!snap.Exists) return null;
        return snap.ConvertTo<Recipe>();
    }
    public async Task<List<string>> ObtenerIdsOrdenadasAsync(CancellationToken ct)
    {
        var docs = await _db.Collection("recetas")
            .OrderBy("titulo")                     // o por fecha, como prefieras
            .GetSnapshotAsync(ct);

        return docs.Documents
                   .Select(d => d.Id)
                   .ToList();
    }
    public async Task<List<string>> BuscarIdsPorTextoAsync(string texto, CancellationToken ct)
    {
        var resultados = await BuscarPorTextoAsync(texto, ct);
        return resultados.Select(r => r["id"].ToString()!).ToList();
    }
    //  BÚSQUEDA EXACTA COMO ANDROID (título + descripción)
    public async Task<List<Dictionary<string, object>>> BuscarPorTextoAsync(string texto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return new List<Dictionary<string, object>>();

        var s = texto.Trim().ToLower();

        // 1) Cargar recetas publicadas (igual que en Android / pantalla principal)
        var snap = await _db.Collection("recetas")
                            .WhereEqualTo("estado", "publicada")
                            .GetSnapshotAsync(ct);

        var list = new List<Dictionary<string, object>>();

        foreach (var doc in snap.Documents)
        {
            var dict = doc.ToDictionary();
            dict["id"] = doc.Id;

            // agregar al índice si coincide título o descripción
            var titulo = dict.TryGetValue("titulo", out var t) ? t?.ToString()?.ToLower() : "";
            var desc = dict.TryGetValue("descripcion", out var d) ? d?.ToString()?.ToLower() : "";

            if ((titulo != null && titulo.Contains(s)) ||
                (desc != null && desc.Contains(s)))
            {
                list.Add(dict);
            }
        }

        return list;
    }
    //public async Task<List<string>> BuscarIdsPorTextoOrdenadosAsync(string texto, CancellationToken ct)
    //{
    //    if (string.IsNullOrWhiteSpace(texto))
    //        return new List<string>();

    //    var s = texto.Trim().ToLower();

    //    // Obtener todas las recetas publicadas
    //    var snap = await _db.Collection("recetas")
    //                        .WhereEqualTo("estado", "publicada")
    //                        .GetSnapshotAsync(ct);

    //    // Filtrar por coincidencias en título o descripción
    //    var list = new List<Dictionary<string, object>>();
    //    foreach (var doc in snap.Documents)
    //    {
    //        var dict = doc.ToDictionary();
    //        dict["id"] = doc.Id;

    //        var titulo = dict.TryGetValue("titulo", out var t) ? t?.ToString()?.ToLower() : "";
    //        var desc = dict.TryGetValue("descripcion", out var d) ? d?.ToString()?.ToLower() : "";

    //        if ((titulo != null && titulo.Contains(s)) ||
    //            (desc != null && desc.Contains(s)))
    //        {
    //            list.Add(dict);
    //        }
    //    }

    //    list = list
    //        .OrderByDescending(r =>
    //        {
    //            var titulo = r.TryGetValue("titulo", out var t) ? t?.ToString()?.ToLower() : "";
    //            return titulo != null && titulo.Contains(s) ? 1 : 0;
    //        })
    //        .ThenByDescending(r =>
    //        {
    //            var pub = r.TryGetValue("publicadoEn", out var p) && p is Timestamp ts ? ts.ToDateTime() : DateTime.MinValue;
    //            return pub;
    //        })
    //        .ToList();

    //    return list.Select(r => r["id"].ToString()!).ToList();
    //}
    public async Task<List<string>> BuscarIdsPorTextoOrdenadosAsync(string q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q))
            return new List<string>();

        var client = new HttpClient();

        // ⚠️ Usa tu dominio correcto:
        string url = $"https://fastapireconocimiento-2.onrender.com/buscar_ids?query={Uri.EscapeDataString(q)}";

        ApiResponse? resp;
        try
        {
            resp = await client.GetFromJsonAsync<ApiResponse>(url, cancellationToken: ct);
        }
        catch
        {
            return new List<string>();
        }

        if (resp == null || resp.resultados == null)
            return new List<string>();

        // ✔ Web navega solo entre los resultados reales filtrados
        return resp.resultados
            .Select(r => r.id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToList();
    }

    public async Task<List<Dictionary<string, object>>> ObtenerDeAutorAsync(
    string uid,
    string? search = null)
    {
        var list = new List<Dictionary<string, object>>();

        if (string.IsNullOrWhiteSpace(uid))
            return list;

        // SOLO recetas cuyo authorUid coincide con este usuario
        var snap = await _db.Collection("recetas")
                            .WhereEqualTo("authorUid", uid)
                            .GetSnapshotAsync();

        foreach (var doc in snap.Documents)
        {
            var dict = doc.ToDictionary();
            dict["id"] = doc.Id;

            if (dict.TryGetValue("publicadoEn", out var pubObj) && pubObj is Timestamp tsPub)
                dict["__orden"] = tsPub.ToDateTime();
            else if (dict.TryGetValue("creadoEn", out var creObj) && creObj is Timestamp tsCre)
                dict["__orden"] = tsCre.ToDateTime();
            else if (doc.CreateTime.HasValue)
                dict["__orden"] = doc.CreateTime.Value.ToDateTime();
            else
                dict["__orden"] = DateTime.MinValue;

            list.Add(dict);
        }

        // Filtro opcional solo dentro de MIS recetas
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            list = list
                .Where(r =>
                    (r.TryGetValue("titulo", out var t) && t?.ToString()?.ToLower().Contains(s) == true) ||
                    (r.TryGetValue("descripcion", out var d) && d?.ToString()?.ToLower().Contains(s) == true)
                )
                .ToList();
        }

        list = list
            .OrderByDescending(d => d["__orden"])
            .ToList();

        foreach (var d in list) d.Remove("__orden");

        return list;
    }

    public async Task<List<Dictionary<string, object>>> ObtenerPorAutorYEstadoAsync(
     string authorUid,
     string estado,
     CancellationToken ct = default)
    {
        var list = new List<Dictionary<string, object>>();

        if (string.IsNullOrWhiteSpace(authorUid))
            return list;

        var query = _db.Collection(Col)
                       .WhereEqualTo("authorUid", authorUid)
                       .WhereEqualTo("estado", estado);

        var snap = await query.GetSnapshotAsync(ct);

        foreach (var doc in snap.Documents)
        {
            if (ct.IsCancellationRequested) break;

            var dict = doc.ToDictionary();
            dict["id"] = doc.Id;

            // orden para poder ordenar si quieres
            if (dict.TryGetValue("creadoEn", out var creObj) && creObj is Timestamp tsCre)
                dict["__orden"] = tsCre.ToDateTime();
            else if (doc.CreateTime.HasValue)
                dict["__orden"] = doc.CreateTime.Value.ToDateTime();
            else
                dict["__orden"] = DateTime.MinValue;

            list.Add(dict);
        }

        // Orden opcional
        list = list
            .OrderByDescending(d => d["__orden"])
            .ToList();

        foreach (var d in list) d.Remove("__orden");

        return list;
    }


}
