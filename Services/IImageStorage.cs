using Microsoft.AspNetCore.Http;

namespace CamCook.Services
{
    public interface IImageStorage
    {
        /// <summary>
        /// Guarda un archivo de imagen y devuelve la URL pública (o null si no hay archivo).
        /// </summary>
        Task<string?> SaveAsync(IFormFile? file, CancellationToken ct = default);
    }
}

