using CamCook.Models;
using CamCook.Models.Api;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;

namespace CamCook.Controllers
{
    [ApiController]
    [Route("")]
    public class RecetasController : ControllerBase
    {
        private readonly FirestoreDb _db;

        public RecetasController(FirestoreDb db)
        {
            _db = db;
        }

        [HttpGet("buscar")]
        public async Task<IActionResult> Buscar(string query)
        {
            // 📌 Obtener todas las recetas
            var recetasSnapshot = await _db.Collection("recetas").GetSnapshotAsync();

            var recetas = recetasSnapshot.Documents
                .Select(d => d.ConvertTo<Recipe>())
                .Where(r => r.Title != null &&
                            r.Title.ToLower().Contains(query.ToLower()))
                .ToList();

            // 📌 Convertir a DTO (para Android y Web)
            var resultado = recetas.Select(r =>
            {
                // -------------------------------
                // 🔥 Selección correcta de imagen
                // -------------------------------
                string? img = r.mainImageUrl;

                if (string.IsNullOrWhiteSpace(img))
                    img = r.imagenUrl;

                if (string.IsNullOrWhiteSpace(img) && r.Steps != null)
                {
                    img = r.Steps
                        .Where(s => !string.IsNullOrWhiteSpace(s.ImageUrl))
                        .Select(s => s.ImageUrl)
                        .FirstOrDefault();
                }

                return new RecetaDto
                {
                    id = r.Id,
                    titulo = r.Title,
                    descripcion = r.Steps?.FirstOrDefault()?.Description ?? "",
                    imagenUrl = img ?? "",
                    calorias = r.Calories ?? 0,
                    porciones = r.Servings ?? 0,
                    tiempoFormateado = r.PrepTimeText,
                    ingredientes = r.Ingredients.Select(i => new IngredienteDto
                    {
                        nombre = i.Name,
                        cantidad = i.Quantity,
                        unidad = i.Unit
                    }).ToList(),
                    pasos = r.Steps.Select(s => new PasoDto
                    {
                        descripcion = s.Description,
                        imagenUrl = s.ImageUrl,
                        orden = s.Order
                    }).ToList()
                };
            }).ToList();

            return Ok(new ApiResponse
            {
                query_original = query,
                total_resultados = resultado.Count,
                resultados = resultado
            });
        }
    }
}
