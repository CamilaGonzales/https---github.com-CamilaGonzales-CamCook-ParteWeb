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
        private readonly ILogger<UpdateModel> _log;

        public UpdateModel(IRecipeRepository repo, ILogger<UpdateModel> log)
        {
            _repo = repo;
            _log = log;
        }

        [BindProperty(SupportsGet = true)]
        public string Id { get; set; } = "";

        [BindProperty]
        public RecipeInput Input { get; set; } = new();

        public string? ExistingMainImageUrl { get; private set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!(User?.Identity?.IsAuthenticated ?? false))
                return RedirectToPage("/Account/Login");

            var recipe = await _repo.GetByIdAsync(Id);
            if (recipe == null)
                return NotFound();

            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue("uid");

            if (recipe.AuthorUid != uid)
                return Forbid();

            ExistingMainImageUrl = recipe.ImageUrl;

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
                    ImageUrl = s.ImageUrl,
                    Order = s.Order
                }).ToList() ?? new List<StepInput>()
            };

            return Page();
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostAsync(CancellationToken ct)
        {
            if (!(User?.Identity?.IsAuthenticated ?? false))
                return RedirectToPage("/Account/Login");

            if (!ModelState.IsValid)
            {
                // Recuperar la imagen actual para volver a mostrarla
                var receta = await _repo.GetByIdAsync(Id, ct);
                ExistingMainImageUrl = receta?.ImageUrl;
                return Page();
            }
            try
            {
                var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? User.FindFirstValue("uid")
                          ?? string.Empty;

                if (string.IsNullOrWhiteSpace(uid))
                {
                    ModelState.AddModelError(string.Empty, "No se pudo determinar el UID del usuario.");
                    return Page();
                }

                // Validación de imágenes vacías
                foreach (var step in Input.Steps)
                {
                    if (step.Image == null || step.Image.Length == 0)
                    {
                        // No se subió nueva imagen, mantener la existente
                        continue;
                    }
                }


                await _repo.UpdateAsync(Id, Input, uid, ct);

                TempData["ok"] = "Cambios enviados a revisión. Te avisaremos cuando se publique.";
                return RedirectToPage("/Recipes/List");
            }
            catch (OperationCanceledException)
            {
                return new StatusCodeResult(StatusCodes.Status499ClientClosedRequest);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error actualizando receta {Id}", Id);
                ModelState.AddModelError(string.Empty, "Ocurrió un error al actualizar la receta. Intenta de nuevo.");
                return Page();
            }
        }
    }
}
