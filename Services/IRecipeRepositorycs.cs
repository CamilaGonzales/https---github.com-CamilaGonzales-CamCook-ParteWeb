using CamCook.Models;

namespace CamCook.Services
{
    public interface IRecipeRepository
    {
        /// <summary>
        /// Crea una receta a partir del formulario y devuelve el Id del documento en Firestore.
        /// </summary>
        Task<string> CreateAsync(RecipeInput input, CancellationToken ct = default);
    }
}
