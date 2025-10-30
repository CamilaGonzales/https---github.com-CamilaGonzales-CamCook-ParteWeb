using Microsoft.AspNetCore.Http;

namespace CamCook.Models;

public class RecipeInput
{
    // Campos del formulario principal
    public string Title { get; set; } = "";
    public int Calories { get; set; }
    public int Servings { get; set; }
    public string PrepTimeText { get; set; } = "";

    // Imagen principal
    public IFormFile? MainImage { get; set; }

    // Listas dinámicas
    public List<IngredientInput> Ingredients { get; set; } = new();
    public List<StepInput> Steps { get; set; } = new();

    public string? AuthorUid { get; set; }
    public string? AuthorEmail { get; set; }
}

public class IngredientInput
{
    public string Name { get; set; } = "";
    public string Quantity { get; set; } = "";
    public string? Unit { get; set; }
}

public class StepInput
{
    public string Description { get; set; } = "";
    public IFormFile? Image { get; set; }
}

