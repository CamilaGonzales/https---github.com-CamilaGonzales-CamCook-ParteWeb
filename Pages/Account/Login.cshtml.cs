using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BCrypt.Net;

namespace CamCook.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly FirestoreDb _db;
        public LoginModel(FirestoreDb db) => _db = db;

        [BindProperty, Required]
        public string Username { get; set; } = "";

        [BindProperty, Required, DataType(DataType.Password)]
        public string Password { get; set; } = "";

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var input = Username.Trim();
            var pass = Password;

            // Buscar usuario por correo o usuario
            QuerySnapshot snap = await _db.Collection("usuarios")
                .WhereEqualTo("correo", input)
                .Limit(1)
                .GetSnapshotAsync();

            if (snap.Count == 0)
            {
                snap = await _db.Collection("usuarios")
                    .WhereEqualTo("usuario", input)
                    .Limit(1)
                    .GetSnapshotAsync();
            }

            if (snap.Count == 0)
            {
                ModelState.AddModelError("", "Usuario o contraseña incorrectos.");
                return Page();
            }

            var doc = snap.Documents.First();
            var data = doc.ToDictionary();

            // Validar estado activo
            if (data.TryGetValue("estado", out var estadoObj) &&
                !string.Equals(estadoObj?.ToString(), "activo", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("", "La cuenta no está activa.");
                return Page();
            }

            // Validar contraseña
            string passwordHash = data.ContainsKey("contraseña")
                ? data["contraseña"].ToString()!
                : data.ContainsKey("password")
                    ? data["password"].ToString()!
                    : "";

            bool ok = passwordHash.StartsWith("$2a$") || passwordHash.StartsWith("$2b$")
                ? BCrypt.Net.BCrypt.Verify(pass, passwordHash)
                : pass == passwordHash;

            if (!ok)
            {
                ModelState.AddModelError("", "Usuario o contraseña incorrectos.");
                return Page();
            }

            // Nombre visible
            string nombre = data.TryGetValue("nombre", out var n) ? n?.ToString() :
                            data.TryGetValue("usuario", out var u) ? u?.ToString() :
                            data.TryGetValue("correo", out var c) ? c?.ToString() : input;

            // Roles
            List<string> roles = new();
            if (data.TryGetValue("rol", out var rolObj))
            {
                if (rolObj is IEnumerable<object> arr)
                    roles = arr.Select(r => r.ToString()).ToList();
                else if (rolObj is string s)
                    roles.Add(s);
            }
            if (roles.Count == 0) roles.Add("usuario");

            // UID real (FireStore uid)
            string uid = data.ContainsKey("uid") ? data["uid"].ToString()! : doc.Id;

            // Crear Claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, uid), // ?? UID real
                new Claim("uid", uid),
                new Claim("firebaseUid", uid),
                new Claim(ClaimTypes.Name, nombre),
                new Claim("email", data.ContainsKey("correo") ? data["correo"].ToString()! : "")
            };

            foreach (var r in roles)
                claims.Add(new Claim(ClaimTypes.Role, r));

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            bool esAdmin = roles.Any(r =>
            string.Equals(r, "admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r, "administrador", StringComparison.OrdinalIgnoreCase));

            if (esAdmin)
                return Redirect("/Admin/Dashboard");   // tu Razor Page de dashboard admin

            return Redirect("/Index");
        }
    }
}
