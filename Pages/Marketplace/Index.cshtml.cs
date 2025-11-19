using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CamCook.Pages.Marketplace
{
    public class IndexModel : PageModel
    {
        private readonly FirestoreDb _db;
        public List<Dictionary<string, object>> Proveedores { get; set; } = new();

        public IndexModel(FirestoreDb db)
        {
            _db = db;
        }

        public async Task OnGet()
        {
            var snap = await _db.Collection("proveedores")
                .WhereEqualTo("estado_validacion", "aprobado")
                .GetSnapshotAsync();

            foreach (var doc in snap.Documents)
            {
                var data = doc.ToDictionary();
                data["proveedorId"] = doc.Id;    // <-- AGREGAR EL ID REAL
                Proveedores.Add(data);
            }

        }
    }
}
