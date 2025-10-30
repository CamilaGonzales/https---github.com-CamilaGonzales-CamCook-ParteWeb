using Microsoft.AspNetCore.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CamCook.Services
{
    public interface IImageStorage
    {
        // Guarda una imagen y devuelve la URL pública
        Task<string> SaveAsync(IFormFile file, CancellationToken ct = default);

        // (Opcional) Sobrecarga si ya tienes bytes (útil para jobs/migraciones)
        Task<string> SaveAsync(byte[] bytes, string contentType = "image/jpeg", string? name = null, CancellationToken ct = default);
    }
}
