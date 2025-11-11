using CamCook.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

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

            var currentUserUid = User.Identity?.Name;
            if (recipe.AuthorUid != currentUserUid)
                return Forbid();

            Title = recipe.Title;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var currentUserUid = User.Identity?.Name;
            try
            {
                await _repo.DeleteAsync(Id, currentUserUid);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch
            {
                return BadRequest();
            }

            return RedirectToPage("/Recipes/List");
        }
    }
}
