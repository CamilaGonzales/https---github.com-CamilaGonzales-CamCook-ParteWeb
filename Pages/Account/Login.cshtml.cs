using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CamCook.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly FirestoreDb _db;
        public LoginModel(FirestoreDb db) => _db = db;

        [BindProperty, Required]
        public string Username { get; set; } = "";  // <- aquí escribes el "nombre" tal como está en Firestore

        [BindProperty, Required, DataType(DataType.Password)]
        public string Password { get; set; } = "";

        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            // ?? Buscar por el campo Firestore "nombre"
            var snap = await _db.Collection("usuarios")
                                .WhereEqualTo("nombre", Username)
                                .Limit(1)
                                .GetSnapshotAsync();

            if (snap.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "Usuario o contraseña incorrectos.");
                return Page();
            }

            var doc = snap.Documents.First();
            var data = doc.ToDictionary();

            // Leer hash de contraseña desde el campo "contraseña"
            if (!data.TryGetValue("contraseña", out var hashObj) || hashObj is null)
            {
                ModelState.AddModelError(string.Empty, "La cuenta no tiene contraseña válida.");
                return Page();
            }

            var passwordHash = hashObj.ToString() ?? "";

            // ? Verificar con BCrypt
            var ok = BCrypt.Net.BCrypt.Verify(Password, passwordHash);
            if (!ok)
            {
                ModelState.AddModelError(string.Empty, "Usuario o contraseña incorrectos.");
                return Page();
            }

            // ?? Claims básicos
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, Username),
                new Claim("uid", doc.Id),
                // Si guardas "rol" en Firestore, descomenta:
                // new Claim(ClaimTypes.Role, data.TryGetValue("rol", out var r) ? r?.ToString() ?? "usuario" : "usuario")
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            // ?? Redirección segura
            if (!string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
                return LocalRedirect(ReturnUrl);

            return RedirectToPage("/Index");
        }
    }
}
