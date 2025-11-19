using Google.Apis.Auth;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace CamCook.Pages.Account
{
    public class LoginFirebaseModel : PageModel
    {
        private readonly FirestoreDb _db;
        public LoginFirebaseModel(FirestoreDb db) => _db = db;

        public class TokenInput { public string Token { get; set; } = ""; }

        public async Task<IActionResult> OnPostAsync([FromBody] TokenInput input)
        {
            if (string.IsNullOrWhiteSpace(input.Token))
                return BadRequest("Token vacío");

            var payload = await GoogleJsonWebSignature.ValidateAsync(input.Token);
            string email = payload.Email.ToLowerInvariant();
            string nombre = payload.Name ?? email;
            string realGoogleUid = payload.Subject; // ESTE ES EL UID CORRECTO

            // BUSCAR POR google_uid REAL
            var snap = await _db.Collection("usuarios")
                .WhereEqualTo("google_uid", realGoogleUid)
                .Limit(1)
                .GetSnapshotAsync();

            DocumentReference docRef;
            Dictionary<string, object> data;

            if (snap.Count > 0)
            {
                docRef = snap.Documents[0].Reference;
                data = snap.Documents[0].ToDictionary();
            }
            else
            {
                // Crear usuario nuevo PERO USANDO EL UID REAL
                var nuevo = new Dictionary<string, object>
        {
            { "usuario", nombre },
            { "email", email },
            { "estado", "activo" },
            { "rol", new List<string>{ "usuario" } },
            { "google_uid", realGoogleUid },
            { "creadoEn", Timestamp.GetCurrentTimestamp() }
        };

                docRef = await _db.Collection("usuarios").AddAsync(nuevo);
                data = nuevo;
            }

            // Claims
            List<string> roles = new();
            if (data.TryGetValue("rol", out var rolObj))
            {
                if (rolObj is IEnumerable<object> arr)
                    roles = arr.Select(x => x.ToString()).ToList();
                else if (rolObj is string s)
                    roles.Add(s);
            }

            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, realGoogleUid),
        new Claim("google_uid", realGoogleUid),
        new Claim(ClaimTypes.Name, nombre),
        new Claim("email", email)
    };
            foreach (var r in roles)
                claims.Add(new Claim(ClaimTypes.Role, r));

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            return new JsonResult(new { ok = true, google_uid = realGoogleUid, roles });
        }

    }
}