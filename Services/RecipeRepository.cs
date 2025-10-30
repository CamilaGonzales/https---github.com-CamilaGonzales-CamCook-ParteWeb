using CamCook.Models;
using Google.Cloud.Firestore;

namespace CamCook.Services
{
    public class RecipeRepository : IRecipeRepository
    {
        private readonly FirestoreDb _db;
        private readonly IImageStorage _imgStore;
        private const string Col = "recetas";

        public RecipeRepository(FirestoreDb db, IImageStorage imgStore)
        {
            _db = db;
            _imgStore = imgStore;
        }

        public async Task<string> CreateAsync(RecipeInput input, CancellationToken ct = default)
        {
            // 1) Subir imagen principal
            var mainUrl = await _imgStore.SaveAsync(input.MainImage, ct);

            // 2) Mapear ingredientes y pasos
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

            // 3) Construir “descripcionCompuesta” para la IA (título + pasos + otros campos)
            var descripcionCompuesta =
                string.Join(" ", steps.Select(x => x.Description ?? string.Empty)) + " " +
                (input.PrepTimeText ?? string.Empty);

            // 4) Ejecutar IA (nivel 1)
            var ai = AnalizadorIA.Analizar(
                titulo: input.Title,
                descripcionCompuesta: descripcionCompuesta,
                ingredientesCount: ingredients.Count,
                tieneImagen: !string.IsNullOrWhiteSpace(mainUrl));

            // 5) Determinar estado inicial (flujo moderación)
            //    - Ok      -> "pendiente_admin"
            //    - Review  -> "revisar"
            //    - Reject  -> "rechazada"
            var estado = ai.Verdict switch
            {
                AiVerdict.Ok => "pendiente_admin",
                AiVerdict.Review => "revisar",
                _ => "rechazada"
            };

            // 6) Guardar en Firestore como diccionario (no requiere tocar el POCO Recipe)
            var docData = new Dictionary<string, object>
            {
                ["titulo"] = input.Title,
                ["calorias"] = input.Calories,
                ["porciones"] = input.Servings,
                ["tiempoPrep"] = input.PrepTimeText,
                ["imagenUrl"] = mainUrl,
                ["creadoEn"] = Timestamp.FromDateTime(DateTime.UtcNow),
                ["ingredientes"] = ingredients.Select(i => new Dictionary<string, object>
                {
                    ["nombre"] = i.Name,
                    ["cantidad"] = i.Quantity,
                    ["unidad"] = i.Unit ?? ""
                }).ToList(),
                ["pasos"] = steps.Select(s => new Dictionary<string, object>
                {
                    ["descripcion"] = s.Description,
                    ["imagenUrl"] = s.ImageUrl ?? "",
                    ["orden"] = s.Order
                }).ToList(),

                // Campos de moderación
                ["estado"] = estado,
                ["ai"] = new Dictionary<string, object>
                {
                    ["score"] = ai.Score,
                    ["flags"] = ai.Flags,
                    ["verdict"] = ai.Verdict.ToString(),
                    ["version"] = 1,
                    ["ts"] = Timestamp.FromDateTime(DateTime.UtcNow)
                },

                // (Opcional) Guarda info del autor si la tenés en el contexto:
                // ["autorUid"]   = autorUid,
                // ["autorEmail"] = autorEmail
            };

            var doc = await _db.Collection(Col).AddAsync(docData, ct);
            return doc.Id;
        }
    }
}
