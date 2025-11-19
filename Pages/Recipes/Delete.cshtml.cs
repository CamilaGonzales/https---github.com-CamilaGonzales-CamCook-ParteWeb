using CamCook.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace CamCook.Pages.Recipes
{
    public class DeleteModel : PageModel
    {
        private readonly IRecipeRepository _repo;

        public DeleteModel(IRecipeRepository repo)
        {
            _repo = repo;
        }

        [BindProperty(SupportsGet = true)]
        public string Id { get; set; } = "";

        public string Title { get; set; } = "";

        public async Task<IActionResult> OnGetAsync()
        {
            var recipe = await _repo.GetByIdAsync(Id);
            if (recipe == null) return NotFound();

            var currentUserUid = User.FindFirstValue(ClaimTypes.NameIdentifier)
                                 ?? User.FindFirstValue("uid");

            if (recipe.AuthorUid != currentUserUid)
                return Forbid();

            Title = recipe.Title;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var currentUserUid = User.FindFirstValue(ClaimTypes.NameIdentifier)
                                 ?? User.FindFirstValue("uid");

            try
            {
                await _repo.DeleteAsync(Id, currentUserUid);
                TempData["ok"] = "Tu receta fue eliminada correctamente.";
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch
            {
                TempData["error"] = "No se pudo eliminar la receta. Intenta de nuevo.";
            }

            // Volvemos a la lista SIEMPRE (no login)
            return RedirectToPage("/Recipes/List");
        }
    }
}
