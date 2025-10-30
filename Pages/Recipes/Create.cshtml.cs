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

        public CreateModel(IRecipeRepository repo)
        {
            _repo = repo;
        }

        [BindProperty]
        public RecipeInput Input { get; set; } = new();

        public IActionResult OnGet()
        {
            if (!(User?.Identity?.IsAuthenticated ?? false))
                return RedirectToPage("/Account/Login");

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(CancellationToken ct)
        {
            if (!(User?.Identity?.IsAuthenticated ?? false))
                return RedirectToPage("/Account/Login");

            if (!ModelState.IsValid) return Page();

            // Tomamos UID y Email del usuario autenticado
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("uid")
                     ?? string.Empty;

            var email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

            if (string.IsNullOrWhiteSpace(uid))
            {
                ModelState.AddModelError(string.Empty, "No se pudo determinar el UID del usuario autenticado.");
                return Page();
            }

            // Inyectamos autor en el modelo antes de crear
            Input.AuthorUid = uid;
            Input.AuthorEmail = string.IsNullOrWhiteSpace(email) ? null : email;

            // Crear receta (el repo hará IA, pondrá estado y publicada=false)
            var id = await _repo.CreateAsync(Input, ct);

            //Mensaje visible en el layout
            TempData["ok"] = "Receta enviada a revisión. Te avisaremos cuando se publique.";

            return RedirectToPage("/Recipes/List");
        }
    }
}
