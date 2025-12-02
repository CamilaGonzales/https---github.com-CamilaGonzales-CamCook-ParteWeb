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

            // Validación para borrador: el título es obligatorio
            if (esBorrador && string.IsNullOrWhiteSpace(Input.Title))
            {
                ModelState.AddModelError(nameof(Input.Title), "El título es obligatorio incluso para guardar como borrador.");
                return Page();
            }

            if (!ModelState.IsValid && !esBorrador)
                return Page();


            try
            {
                var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? User.FindFirstValue("uid")
                          ?? string.Empty;

                var email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
                var nombre = User.FindFirstValue(ClaimTypes.Name)
                             ?? User.FindFirstValue("nombre")
                             ?? string.Empty;

                if (string.IsNullOrWhiteSpace(uid))
                {
                    ModelState.AddModelError(string.Empty, "No se pudo determinar el UID del usuario autenticado.");
                    return Page();
                }

                // Inyectamos datos del autor
                Input.AuthorUid = uid;
                Input.AuthorEmail = string.IsNullOrWhiteSpace(email) ? null : email;
                Input.AuthorName = string.IsNullOrWhiteSpace(nombre) ? null : nombre;

                string id;

                if (esBorrador)
                {
                    // Guardar como borrador siempre (incluso si hay validaciones pendientes)
                    id = await _repo.CreateDraftAsync(Input, ct);

                    // Si el modelo no es válido, queremos que el usuario vea las validaciones
                    // pero igualmente guardamos el borrador. En ese caso, permanecemos en la página.
                    if (!ModelState.IsValid)
                    {
                        TempData["ok"] = "Receta guardada como borrador. Atención: hay validaciones pendientes que deben revisarse antes de enviar a revisión.";
                        return Page();
                    }

                    TempData["ok"] = "Receta guardada como borrador. Puedes editarla o enviarla a revisión cuando quieras.";
                }
                else
                {
                    // Enviar a revisión (flujo normal) — requiere modelo válido (ya chequeado arriba)
                    id = await _repo.CreateAsync(Input, ct);
                    TempData["ok"] = "Receta enviada a revisión. Te avisaremos cuando se publique.";
                }

                return RedirectToPage("/Recipes/List");
            }
            catch (OperationCanceledException)
            {
                return new StatusCodeResult(StatusCodes.Status499ClientClosedRequest);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error creando receta");
                ModelState.AddModelError(string.Empty, "Ocurri� un error al crear la receta. Intenta nuevamente.");
                return Page();
            }
        }
    }
}
