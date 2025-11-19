using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace CamCook.Pages.Proveedor
{
    public class MisProductosModel : PageModel
    {
        private readonly FirestoreDb _db;
        public List<Dictionary<string, object>> Productos { get; set; } = new();
        public string MensajeError { get; set; } = string.Empty;

        public MisProductosModel(FirestoreDb db) => _db = db;

        public async Task<IActionResult> OnGet()
        {
            var googleUid = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(googleUid))
            {
                MensajeError = "Error: no se pudo obtener tu UID. Vuelve a iniciar sesión.";
                return Page();
            }

            Console.WriteLine("UID Claim: " + googleUid);

            // ? Buscar proveedor DIRECTAMENTE por UID (que es el ID del documento)
            var proveedorDoc = await _db.Collection("proveedores")
                .Document(googleUid)
                .GetSnapshotAsync();

            if (!proveedorDoc.Exists)
            {
                MensajeError = "No eres un proveedor aprobado o tu UID no coincide.";
                return Page();
            }

            if (!proveedorDoc.ContainsField("estado_validacion") ||
                proveedorDoc.GetValue<string>("estado_validacion") != "aprobado")
            {
                MensajeError = "No eres un proveedor aprobado.";
                return Page();
            }

            // ? Cargar productos del proveedor
            var productosSnap = await _db.Collection("proveedores")
                .Document(googleUid)
                .Collection("productos")
                .GetSnapshotAsync();

            if (productosSnap.Count == 0)
            {
                MensajeError = "No se encontraron productos para tu proveedor.";
            }

            foreach (var doc in productosSnap.Documents)
            {
                var data = doc.ToDictionary();

                if (!data.ContainsKey("imagen") || string.IsNullOrWhiteSpace(data["imagen"]?.ToString()))
                    data["imagen"] = "/img/default.png";

                Productos.Add(data);
            }

            return Page();
        }

    }
}