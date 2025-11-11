using Microsoft.AspNetCore.Http;

namespace CamCook.Services
{
    public interface IImageStorage
    {
        /// <summary>Guarda la imagen y devuelve la URL pública.</summary>
        Task<string> SaveAsync(IFormFile file, CancellationToken ct = default);
    }
}
