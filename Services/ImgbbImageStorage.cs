using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace CamCook.Services
{
    public class ImgbbImageStorage : IImageStorage
    {
        private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg", "image/jpg", "image/png", "image/webp", "image/gif"
        };

        private readonly IHttpClientFactory _httpFactory;
        private readonly string _apiKey;

        public ImgbbImageStorage(IHttpClientFactory httpFactory, IConfiguration config)
        {
            _httpFactory = httpFactory;
            _apiKey = config["Imgbb:ApiKey"]
                ?? throw new InvalidOperationException("Falta Imgbb:ApiKey en configuración.");
        }

        public async Task<string> SaveAsync(IFormFile file, CancellationToken ct = default)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("Archivo vacío.");

            var contentType = file.ContentType ?? "";
            if (!Allowed.Contains(contentType))
                throw new InvalidOperationException($"Tipo de imagen no permitido: {contentType}");

            var client = _httpFactory.CreateClient("imgbb");

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            ms.Position = 0;

            using var content = new MultipartFormDataContent();

            // API key
            content.Add(new StringContent(_apiKey), "key");

            // (Opcional) nombre lógico sin espacios raros
            var safeName = Path.GetFileNameWithoutExtension(file.FileName);
            safeName = string.Join("-", safeName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
            if (!string.IsNullOrWhiteSpace(safeName))
                content.Add(new StringContent(safeName), "name");

            // archivo binario
            var fileContent = new StreamContent(ms);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            content.Add(fileContent, "image", file.FileName);

            var response = await client.PostAsync("1/upload", content, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"ImgBB error {response.StatusCode}: {body}");

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            var url = root.GetProperty("data").GetProperty("url").GetString()
                      ?? root.GetProperty("data").GetProperty("display_url").GetString();

            if (string.IsNullOrWhiteSpace(url))
                throw new InvalidOperationException("ImgBB no devolvió URL.");

            return url!;
        }
    }
}
