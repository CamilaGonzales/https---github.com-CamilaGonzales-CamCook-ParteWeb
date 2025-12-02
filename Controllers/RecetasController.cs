using CamCook.Models;
using CamCook.Models.Api;
using CamCook.Services;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CamCook.Controllers
{
    [ApiController]
    [Route("")]
    public class RecetasController : ControllerBase
    {
        private readonly FirestoreDb _db;
        private readonly RecetaService _svc;

        public RecetasController(FirestoreDb db, RecetaService svc)
        {
            _db = db;
            _svc = svc;
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

        // ==================== LIKES Y VIEWS ====================

        [HttpPost("api/recetas/{recetaId}/like")]
        public async Task<IActionResult> AddLike(string recetaId, CancellationToken ct)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                       ?? User.FindFirstValue("uid");

            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new { error = "Usuario no autenticado" });

            try
            {
                await _svc.AddLikeAsync(recetaId, userId, ct);

                // Leer conteo actualizado
                var snap = await _db.Collection("recetas").Document(recetaId).GetSnapshotAsync(ct);
                var dict = snap.Exists ? snap.ToDictionary() : new Dictionary<string, object>();
                int likes = 0;
                if (dict.TryGetValue("likes", out var l) && l != null)
                {
                    if (l is int li) likes = li;
                    else if (l is long ll) likes = (int)ll;
                    else if (int.TryParse(l.ToString(), out var lp)) likes = lp;
                }

                return Ok(new { success = true, message = "Like agregado", likes });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("api/recetas/{recetaId}/unlike")]
        public async Task<IActionResult> RemoveLike(string recetaId, CancellationToken ct)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                       ?? User.FindFirstValue("uid");

            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new { error = "Usuario no autenticado" });

            try
            {
                await _svc.RemoveLikeAsync(recetaId, userId, ct);

                // Leer conteo actualizado
                var snap = await _db.Collection("recetas").Document(recetaId).GetSnapshotAsync(ct);
                var dict = snap.Exists ? snap.ToDictionary() : new Dictionary<string, object>();
                int likes = 0;
                if (dict.TryGetValue("likes", out var l) && l != null)
                {
                    if (l is int li) likes = li;
                    else if (l is long ll) likes = (int)ll;
                    else if (int.TryParse(l.ToString(), out var lp)) likes = lp;
                }

                return Ok(new { success = true, message = "Like removido", likes });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("api/recetas/{recetaId}/liked")]
        public async Task<IActionResult> IsLiked(string recetaId, CancellationToken ct)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                       ?? User.FindFirstValue("uid");

            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new { error = "Usuario no autenticado" });

            try
            {
                var liked = await _svc.UserLikedRecipeAsync(recetaId, userId, ct);
                return Ok(new { liked });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
