using Microsoft.AspNetCore.Http;
using System.Net.Http.Headers;

namespace CamCook.Services
{
    public class ImgbbImageStorage : IImageStorage
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;

        public ImgbbImageStorage(HttpClient http, IConfiguration cfg)
        {
            _http = http;
            _apiKey = cfg["Imgbb:ApiKey"] ?? throw new InvalidOperationException("Imgbb:ApiKey no configurada");
        }

        public async Task<string> SaveAsync(IFormFile file, CancellationToken ct = default)
        {
            if (file is null || file.Length == 0) throw new ArgumentException("Archivo vacío");
            if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Solo se permiten imágenes");

            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms, ct);
                bytes = ms.ToArray();
            }
            // nombre opcional: usa el nombre del archivo sin espacios
            var name = Path.GetFileNameWithoutExtension(file.FileName)?.Replace(' ', '_');
            return await SaveAsync(bytes, file.ContentType, name, ct);
        }

        public async Task<string> SaveAsync(byte[] bytes, string contentType = "image/jpeg", string? name = null, CancellationToken ct = default)
        {
            if (bytes == null || bytes.Length == 0) throw new ArgumentException("Bytes vacíos");
            var b64 = Convert.ToBase64String(bytes);

            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(_apiKey), "key");
            form.Add(new StringContent(b64), "image");
            if (!string.IsNullOrWhiteSpace(name))
                form.Add(new StringContent(name), "name");

            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.imgbb.com/1/upload")
            { Content = form };
            req.Headers.UserAgent.Add(new ProductInfoHeaderValue("CamCook", "1.0"));

            var resp = await _http.SendAsync(req, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);
            resp.EnsureSuccessStatusCode();

            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var url = doc.RootElement.GetProperty("data").GetProperty("url").GetString();
            if (string.IsNullOrWhiteSpace(url)) throw new InvalidOperationException("Imgbb no devolvió URL.");
            return url!;
        }
    }
}
