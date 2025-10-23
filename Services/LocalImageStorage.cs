using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace CamCook.Services
{
    public class LocalImageStorage : IImageStorage
    {
        private readonly IWebHostEnvironment _env;
        private readonly IHttpContextAccessor _http;

        public LocalImageStorage(IWebHostEnvironment env, IHttpContextAccessor http)
        {
            _env = env;
            _http = http;
        }

        public async Task<string?> SaveAsync(IFormFile? file, CancellationToken ct = default)
        {
            if (file == null || file.Length == 0) return null;

            var uploadsPath = Path.Combine(_env.WebRootPath, "uploads");
            Directory.CreateDirectory(uploadsPath);

            var ext = Path.GetExtension(file.FileName);
            var name = $"{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(uploadsPath, name);

            using var fs = new FileStream(fullPath, FileMode.Create);
            await file.CopyToAsync(fs, ct);

            // URL pública en desarrollo
            var req = _http.HttpContext!;
            var baseUrl = $"{req.Request.Scheme}://{req.Request.Host}";
            return $"{baseUrl}/uploads/{name}";
        }
    }
}
