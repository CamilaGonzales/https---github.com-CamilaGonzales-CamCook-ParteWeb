using CamCook.Models;

namespace CamCook.Services
{
    public interface IRecipeRepository
    {
        /// <summary>
        /// Crea una receta a partir del formulario y devuelve el Id del documento en Firestore.
        /// </summary>
        Task<string> CreateAsync(RecipeInput input, CancellationToken ct = default);
        Task<List<Recipe>> GetAllAsync(CancellationToken ct = default);
        Task<Recipe?> GetByIdAsync(string id, CancellationToken ct = default);
        Task UpdateAsync(string id, RecipeInput input, string currentUserUid, CancellationToken ct = default);
        Task DeleteAsync(string id, string currentUserUid, CancellationToken ct = default);
    }
}
