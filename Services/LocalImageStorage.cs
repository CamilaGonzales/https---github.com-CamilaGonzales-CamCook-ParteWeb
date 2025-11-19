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

        public async Task<string> SaveAsync(IFormFile file, CancellationToken ct = default)
        {
            if (file == null || file.Length == 0) throw new ArgumentException("Archivo vacío");

            var uploads = Path.Combine(_env.WebRootPath, "uploads");
            Directory.CreateDirectory(uploads);

            var fileName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
            var fullPath = Path.Combine(uploads, fileName);
            using (var fs = new FileStream(fullPath, FileMode.Create))
                await file.CopyToAsync(fs, ct);

            var req = _http.HttpContext!.Request;
            var baseUrl = $"{req.Scheme}://{req.Host}";
            return $"{baseUrl}/uploads/{fileName}";
        }

        public async Task<string> SaveAsync(byte[] bytes, string contentType = "image/jpeg", string? name = null, CancellationToken ct = default)
        {
            var uploads = Path.Combine(_env.WebRootPath, "uploads");
            Directory.CreateDirectory(uploads);

            var fileName = $"{(string.IsNullOrWhiteSpace(name) ? Guid.NewGuid().ToString("N") : name)}.jpg";
            var fullPath = Path.Combine(uploads, fileName);
            await File.WriteAllBytesAsync(fullPath, bytes, ct);

            var req = _http.HttpContext!.Request;
            var baseUrl = $"{req.Scheme}://{req.Host}";
            return $"{baseUrl}/uploads/{fileName}";
        }
    }
}
