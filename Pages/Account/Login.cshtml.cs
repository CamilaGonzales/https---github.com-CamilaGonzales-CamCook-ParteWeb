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

        [BindProperty, Required(ErrorMessage = "Ingresa tu usuario o correo.")]

        public string Username { get; set; } = "";

        [BindProperty, Required(ErrorMessage = "Ingresa tu contraseña."), DataType(DataType.Password)]

        public string Password { get; set; } = "";

        [BindProperty(SupportsGet = true)]

        public string? ReturnUrl { get; set; }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()

        {

            if (!ModelState.IsValid) return Page();

            var input = (Username ?? "").Trim();

            var pass = Password ?? "";

            // Buscar usuario por nombre o correo

            QuerySnapshot snap = await _db.Collection("usuarios")

                .WhereEqualTo("usuario", input)

                .Limit(1)

                .GetSnapshotAsync();

            if (snap.Count == 0)

            {

                snap = await _db.Collection("usuarios")

                    .WhereEqualTo("correo", input)

                    .Limit(1)

                    .GetSnapshotAsync();

            }

            if (snap.Count == 0)

            {

                ModelState.AddModelError(string.Empty, "Usuario o contraseña incorrectos.");

                return Page();

            }

            var doc = snap.Documents.First();

            var data = doc.ToDictionary();

            // Estado activo

            if (data.TryGetValue("estado", out var estadoObj) &&

                !string.Equals(estadoObj?.ToString(), "activo", StringComparison.OrdinalIgnoreCase))

            {

                ModelState.AddModelError(string.Empty, "La cuenta no está activa.");

                return Page();

            }

            // Compatibilidad: buscar contraseña en "contraseña" o "password"

            if (!data.TryGetValue("contraseña", out var hashObj) && !data.TryGetValue("password", out hashObj))

            {

                ModelState.AddModelError(string.Empty, "No se encontró contraseña válida en la cuenta.");

                return Page();

            }

            var passwordHash = hashObj?.ToString() ?? "";

            bool ok;

            // Compatibilidad con cuentas antiguas (texto plano)

            if (passwordHash.StartsWith("$2a$") || passwordHash.StartsWith("$2b$"))

            {

                ok = BCrypt.Net.BCrypt.Verify(pass, passwordHash);

            }

            else

            {

                ok = pass == passwordHash;

            }

            if (!ok)

            {

                ModelState.AddModelError(string.Empty, "Usuario o contraseña incorrectos.");

                return Page();

            }

            // Intentar obtener el nombre real del usuario

            var nombreVisible =

                (data.TryGetValue("nombre", out var n) ? n?.ToString() :

                 data.TryGetValue("usuario", out var u) ? u?.ToString() :

                 data.TryGetValue("correo", out var c) ? c?.ToString() : input) ?? input;


            var rol = data.TryGetValue("rol", out var r) ? (r?.ToString() ?? "usuario") : "usuario";

            var claims = new List<Claim>

            {

                new Claim(ClaimTypes.Name, nombreVisible),     // Nombre legible del usuario

                new Claim(ClaimTypes.NameIdentifier, doc.Id),  // UID estándar

                new Claim("uid", doc.Id),                      // UID personalizado (compatibilidad)

                new Claim(ClaimTypes.Role, rol)

            };


            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            if (!string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))

                return LocalRedirect(ReturnUrl);

            return RedirectToPage("/Index");

        }

    }

}

