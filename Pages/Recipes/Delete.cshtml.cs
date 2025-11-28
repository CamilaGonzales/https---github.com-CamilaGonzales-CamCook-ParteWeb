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
                TempData["error"] = "No puedes eliminar recetas de otros usuarios.";
            }
            catch
            {
                TempData["error"] = "No se pudo eliminar la receta. Intenta de nuevo.";
            }

            return RedirectToPage("/Recipes/List");
        }
    }
}
