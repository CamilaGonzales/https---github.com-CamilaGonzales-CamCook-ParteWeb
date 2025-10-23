using CamCook.Models;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CamCook.Pages.Recipes
{
    [Authorize] // exige login
    public class ListModel : PageModel
    {
        private readonly FirestoreDb _db;
        public List<Recipe> Recetas { get; set; } = new();

        public ListModel(FirestoreDb db) => _db = db;

        public async Task OnGet(string? search = null)
        {
            Query query = _db.Collection("recetas");
            if (!string.IsNullOrWhiteSpace(search))
            {
                // Búsqueda simple por título (si guardaste el campo "titulo")
                query = query.WhereEqualTo("titulo", search);
            }

            var snap = await query.GetSnapshotAsync();
            foreach (var doc in snap.Documents)
            {
                var r = doc.ConvertTo<Recipe>();
                Recetas.Add(r);
            }
        }
    }
}
