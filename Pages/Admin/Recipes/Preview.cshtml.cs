using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CamCook.Pages.Admin.Recipes;

[Authorize(Roles = "admin")]
public class PreviewModel : PageModel
{
    private readonly FirestoreDb _db;
    public PreviewModel(FirestoreDb db) => _db = db;

    [BindProperty(SupportsGet = true)]
    public string Id { get; set; } = default!;

    public string? Titulo { get; set; }
    public string? AutorEmail { get; set; }
    public string? ImagenUrl { get; set; }
    public int? Calories { get; set; }
    public int? Servings { get; set; }
    public string? PrepTimeText { get; set; }
    public string Estado { get; set; } = "pendiente";
    public string? MotivoRechazo { get; set; }

    public List<string> Ingredientes { get; set; } = new();
    public List<string> Pasos { get; set; } = new();
    public List<string> Tags { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        if (string.IsNullOrWhiteSpace(Id)) return NotFound();

        var doc = await _db.Collection("recetas").Document(Id).GetSnapshotAsync();
        if (!doc.Exists) return NotFound();

        // Datos principales
        Titulo = doc.TryGetValue("titulo", out string t) ? t : null;
        AutorEmail = doc.TryGetValue("authorEmail", out string ae) ? ae : null;
        ImagenUrl = doc.TryGetValue("imagenUrl", out string im) ? im : null;
        Calories = doc.TryGetValue("calorias", out int cal) ? cal : (int?)null;
        Servings = doc.TryGetValue("porciones", out int sv) ? sv : (int?)null;
        PrepTimeText = doc.TryGetValue("tiempoPrep", out string pt) ? pt : null;
        Estado = doc.TryGetValue("estado", out string es) ? es : Estado;
        MotivoRechazo = doc.TryGetValue("motivoRechazo", out string mr) ? mr : null;

        // INGREDIENTES
        if (doc.TryGetValue("ingredientes", out IEnumerable<Dictionary<string, object>> ingList))
        {
            foreach (var ing in ingList)
            {
                var n = ing.TryGetValue("nombre", out object? nom) ? nom?.ToString() : null;
                var q = ing.TryGetValue("cantidad", out object? cant) ? cant?.ToString() : null;
                var u = ing.TryGetValue("unidad", out object? uni) ? uni?.ToString() : null;

                Ingredientes.Add($"{n} {(q ?? "")} {(u ?? "")}".Trim());
            }
        }

        // PASOS
        if (doc.TryGetValue("pasos", out IEnumerable<Dictionary<string, object>> pasoList))
        {
            foreach (var p in pasoList)
            {
                var desc = p.TryGetValue("descripcion", out object? d) ? d?.ToString() : null;
                Pasos.Add(desc ?? "(Sin descripción)");
            }
        }

        // TAGS (si son strings)
        if (doc.TryGetValue("tags", out IEnumerable<string> tg))
            Tags = tg.ToList();

        return Page();
    }
}
