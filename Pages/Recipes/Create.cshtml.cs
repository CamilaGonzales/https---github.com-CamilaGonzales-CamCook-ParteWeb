using CamCook.Models;
using CamCook.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace CamCook.Pages.Recipes
{
    [Authorize]
    public class CreateModel : PageModel
    {
        private readonly IRecipeRepository _repo;
        private readonly ILogger<CreateModel> _log;

        public CreateModel(IRecipeRepository repo, ILogger<CreateModel> log)
        {
            _repo = repo;
            _log = log;
        }

        [BindProperty]
        public RecipeInput Input { get; set; } = new();

        public IActionResult OnGet()
        {
            if (!(User?.Identity?.IsAuthenticated ?? false))
                return RedirectToPage("/Account/Login");

            return Page();
        }

        // ?? BOT�N "Guardar" (borrador)
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostGuardarAsync(CancellationToken ct)
        {
            return await CrearRecetaInternoAsync(ct, esBorrador: true);
        }
         
        // BOT�N "Mandar a revisar"
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostEnviarAsync(CancellationToken ct)
        {
            return await CrearRecetaInternoAsync(ct, esBorrador: false);
        }

        // ELIMINA tu antiguo OnPostAsync, ya no se usa

        // LÓGICA COMPARTIDA
        private async Task<IActionResult> CrearRecetaInternoAsync(CancellationToken ct, bool esBorrador)
        {
            if (!(User?.Identity?.IsAuthenticated ?? false))
                return RedirectToPage("/Account/Login");

            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue("uid")
                      ?? string.Empty;

            if (string.IsNullOrWhiteSpace(uid))
            {
                ModelState.AddModelError(string.Empty, "No se pudo determinar el UID del usuario autenticado.");
                return Page();
            }

            // Validación para borrador: el título es obligatorio
            if (esBorrador && string.IsNullOrWhiteSpace(Input.Title))
            {
                ModelState.AddModelError(nameof(Input.Title), "El título es obligatorio incluso para guardar como borrador.");
                return Page();
            }

            // Enviar a revisión requiere modelo válido
            if (!ModelState.IsValid && !esBorrador)
                return Page();

            // ✅ Bloquear recetas duplicadas por título (por usuario)
            var normalizedTitle = NormalizeTitle(Input.Title);
            if (!string.IsNullOrWhiteSpace(normalizedTitle))
            {
                var exists = await _repo.ExistsByTitleAsync(uid, normalizedTitle, ct);
                if (exists)
                {
                    TempData["swal_error"] = "Esta receta ya fue creada.";
                    return Page(); // para mostrar el SweetAlert
                }
            }

            try
            {
                var email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
                var nombre = User.FindFirstValue(ClaimTypes.Name)
                             ?? User.FindFirstValue("nombre")
                             ?? string.Empty;

                // Inyectamos datos del autor
                Input.AuthorUid = uid;
                Input.AuthorEmail = string.IsNullOrWhiteSpace(email) ? null : email;
                Input.AuthorName = string.IsNullOrWhiteSpace(nombre) ? null : nombre;

                // ✅ Guardar normalizado (para futuras comparaciones)
                Input.TitleNormalized = normalizedTitle;

                string id;

                if (esBorrador)
                {
                    id = await _repo.CreateDraftAsync(Input, ct);
                    TempData["ok"] = "Receta guardada como borrador.";
                }
                else
                {
                    id = await _repo.CreateAsync(Input, ct);
                    TempData["ok"] = "Receta enviada a revisión.";
                }

                return RedirectToPage("/Index");
            }
            catch (OperationCanceledException)
            {
                return new StatusCodeResult(StatusCodes.Status499ClientClosedRequest);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error creando receta");
                ModelState.AddModelError(string.Empty, "Ocurrió un error al crear la receta. Intenta nuevamente.");
                return Page(); // mejor Page() para ver el error, no redirigir
            }
        }


        private static string NormalizeTitle(string? t)
        {
            if (string.IsNullOrWhiteSpace(t)) return "";
            t = t.Trim();
            t = System.Text.RegularExpressions.Regex.Replace(t, @"\s+", " ");
            return t.ToLowerInvariant();
        }
    }
}
