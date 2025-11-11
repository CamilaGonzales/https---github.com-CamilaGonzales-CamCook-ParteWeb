using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace CamCook.Models
{
    public class RecipeInput
    {
        [Required(ErrorMessage = "El título es obligatorio.")]
        public string? Title { get; set; }

        [Range(0, 50000)]
        public int? Calories { get; set; }

        [Range(0, 1000)]
        public int? Servings { get; set; }

        public string? PrepTimeText { get; set; }

        public IFormFile? MainImage { get; set; }

        public List<IngredientInput> Ingredients { get; set; } = new();
        public List<StepInput> Steps { get; set; } = new();

        // se inyectan en el PageModel antes de crear
        public string? AuthorUid { get; set; }
        public string? AuthorEmail { get; set; }
        public string? AuthorName { get; set; }

    }

    public class IngredientInput
    {
        public string? Name { get; set; }
        public string? Quantity { get; set; }
        public string? Unit { get; set; }
    }

    public class StepInput
    {
        public string? Description { get; set; }
        public IFormFile? Image { get; set; }
        public string? ImageUrl { get; set; }
        public int Order { get; set; }
    }
}