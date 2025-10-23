using CamCook.Models;
using CamCook.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

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

        public void OnGet()
        {
            if (Input.Ingredients.Count == 0) Input.Ingredients.Add(new IngredientInput());
            if (Input.Steps.Count == 0) Input.Steps.Add(new StepInput());
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var id = await _repo.CreateAsync(Input);
            // TODO: crea una página Details y redirige ahí si quieres
            return RedirectToPage("/Index");
        }
    }
}
