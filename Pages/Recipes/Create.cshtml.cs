using CamCook.Models;
using CamCook.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace CamCook.Pages.Recipes
{
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

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostAsync(CancellationToken ct)
        {
            if (!(User?.Identity?.IsAuthenticated ?? false))
                return RedirectToPage("/Account/Login");

            if (!ModelState.IsValid)
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

                var id = await _repo.CreateAsync(Input, ct);

                TempData["ok"] = "Receta enviada a revisión. Te avisaremos cuando se publique.";
                return RedirectToPage("/Recipes/List");
            }
            catch (OperationCanceledException)
            {
                return new StatusCodeResult(StatusCodes.Status499ClientClosedRequest);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error creando receta");
                ModelState.AddModelError(string.Empty, "Ocurrió un error al crear la receta. Intenta nuevamente.");
                return Page();
            }
        }

    }
}
