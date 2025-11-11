using CamCook.Models;
using CamCook.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace CamCook.Pages.Recipes
{
    public class UpdateModel : PageModel
    {
        private readonly IRecipeRepository _repo;
        public UpdateModel(IRecipeRepository repo) => _repo = repo;

        [BindProperty] public RecipeInput Input { get; set; } = new();

        // Para vista previa de imagen principal
        public string? ExistingMainImageUrl { get; private set; }

        [BindProperty(SupportsGet = true)]
        public string Id { get; set; } = "";

        public async Task<IActionResult> OnGetAsync()
        {
            var recipe = await _repo.GetByIdAsync(Id);
            if (recipe == null) return NotFound();

            // Solo el autor puede editar
            var currentUserUid = User.FindFirstValue(ClaimTypes.NameIdentifier)
                                ?? User.FindFirstValue("uid");
            if (recipe.AuthorUid != currentUserUid) return Forbid();

            ExistingMainImageUrl = recipe.ImageUrl;

            // Mapear a Input
            Input = new RecipeInput
            {
                Title = recipe.Title,
                Calories = recipe.Calories,
                Servings = recipe.Servings,
                PrepTimeText = recipe.PrepTimeText,
                Ingredients = recipe.Ingredients?.Select(i => new IngredientInput
                {
                    Name = i.Name,
                    Quantity = i.Quantity,
                    Unit = i.Unit
                }).ToList() ?? new List<IngredientInput>(),
                Steps = recipe.Steps?.OrderBy(s => s.Order).Select(s => new StepInput
                {
                    Description = s.Description,
                    ImageUrl = s.ImageUrl,   // para mostrar y conservar
                    Order = s.Order
                }).ToList() ?? new List<StepInput>()
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var currentUserUid = User.FindFirstValue(ClaimTypes.NameIdentifier)
                                ?? User.FindFirstValue("uid");

            try
            {
                await _repo.UpdateAsync(Id, Input, currentUserUid); // hará “revisar”
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch { return BadRequest(); }

            TempData["ok"] = "Cambios enviados a revisión. Te avisaremos cuando se publique.";
            return RedirectToPage("/Recipes/List");
        }
    }
}
