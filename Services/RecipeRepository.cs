// Services/RecipeRepository.cs
using CamCook.Models;
using Google.Cloud.Firestore;

namespace CamCook.Services
{
    public class RecipeRepository : IRecipeRepository
    {
        private readonly FirestoreDb _db;
        private readonly IImageStorage _imgStore;

        // cambia "recipes" por "recetas"
        private const string Col = "recetas";

        public RecipeRepository(FirestoreDb db, IImageStorage imgStore)
        {
            _db = db;
            _imgStore = imgStore;
        }

        public async Task<string> CreateAsync(RecipeInput input, CancellationToken ct = default)
        {
            var mainUrl = await _imgStore.SaveAsync(input.MainImage, ct);

            var ingredients = input.Ingredients.Select(i => new Ingredient
            {
                Name = i.Name,
                Quantity = i.Quantity,
                Unit = i.Unit
            }).ToList();

            var steps = new List<CookStep>();
            for (int i = 0; i < input.Steps.Count; i++)
            {
                var s = input.Steps[i];
                var stepUrl = await _imgStore.SaveAsync(s.Image, ct);

                steps.Add(new CookStep
                {
                    Description = s.Description,
                    ImageUrl = stepUrl,
                    Order = i + 1
                });
            }

            var recipe = new Recipe
            {
                Title = input.Title,
                Calories = input.Calories,
                Servings = input.Servings,
                PrepTimeText = input.PrepTimeText,
                ImageUrl = mainUrl,
                CreatedAt = Timestamp.FromDateTime(DateTime.UtcNow),
                Ingredients = ingredients,
                Steps = steps
            };

            var doc = await _db.Collection(Col).AddAsync(recipe, ct);
            return doc.Id;
        }
    }
}
