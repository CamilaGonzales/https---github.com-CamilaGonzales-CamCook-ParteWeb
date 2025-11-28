using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CamCook.Pages.Marketplace;

public class ProveedorDetalleModel : PageModel
{
    private readonly FirestoreDb _db;

    public ProveedorDetalleModel(FirestoreDb db)
    {
        _db = db;
    }

    [BindProperty(SupportsGet = true)]
    public string Id { get; set; } = "";

    public ProveedorVm? Proveedor { get; set; }

    public async Task OnGet()
    {
        if (string.IsNullOrWhiteSpace(Id)) return;

        var docSnap = await _db.Collection("proveedores").Document(Id).GetSnapshotAsync();
        if (!docSnap.Exists) return;

        var data = docSnap.ToDictionary();

        // Cargar productos
        var productosSnap = await _db.Collection("proveedores")
            .Document(Id)
            .Collection("productos")
            .GetSnapshotAsync();


        var productos = productosSnap.Documents.Select(prodDoc =>
        {
            var prodData = prodDoc.ToDictionary();
            return new ProductoVm
            {
                Id = prodDoc.Id,
                Nombre = prodData.GetValueOrDefault("nombre")?.ToString() ?? "",
                Descripcion = prodData.GetValueOrDefault("descripcion")?.ToString() ?? "",
                Imagen = prodData.GetValueOrDefault("imagen")?.ToString() ?? "",
                Precio = prodData.GetValueOrDefault("precio")?.ToString() ?? "",
                Tipo = prodData.GetValueOrDefault("tipo")?.ToString() ?? "",
                RecetaId = prodData.GetValueOrDefault("recetaId")?.ToString() ?? "",
                Cantidad = prodData.GetValueOrDefault("cantidad")?.ToString() ?? "",
                Unidad = prodData.GetValueOrDefault("unidad")?.ToString() ?? ""
            };
        }).ToList();

        Proveedor = new ProveedorVm
        {
            Id = docSnap.Id,
            Nombre = data.GetValueOrDefault("nombre")?.ToString() ?? "",
            Telefono = data.GetValueOrDefault("telefono")?.ToString() ?? "",
            Tipo = data.GetValueOrDefault("tipoProveedor")?.ToString() ?? "",
            Ciudad = data.GetValueOrDefault("ciudad")?.ToString() ?? "",
            Productos = productos
        };
    }

    public class ProveedorVm
    {
        public string Id { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string Telefono { get; set; } = "";
        public string Tipo { get; set; } = "";
        public string Ciudad { get; set; } = "";
        public List<ProductoVm> Productos { get; set; } = new();
    }

    public class ProductoVm
    {
        public string Id { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public string Imagen { get; set; } = "";
        public string Precio { get; set; } = "";
        public string Tipo { get; set; } = "";
        public string RecetaId { get; set; } = "";
        public string Cantidad { get; set; } = "";
        public string Unidad { get; set; } = "";
    }
}
