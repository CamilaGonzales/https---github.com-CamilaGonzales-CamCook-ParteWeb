using System.Security.Claims;
using BCrypt.Net;
using CamCook.Models;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace CamCook.Controllers;
[Route("Account")]
[ApiController] // <-- IMPORTANTE
public class AccountController : Controller
{
    private readonly FirestoreDb _db;
    public AccountController(FirestoreDb db) => _db = db;

    [HttpGet] public IActionResult Login() => View(new LoginViewModel());

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        // 🔎 busca por el campo español "usuario"
        var q = _db.Collection("usuarios")
                   .WhereEqualTo("usuario", vm.Username)
                   .Limit(1);

        var snap = await q.GetSnapshotAsync();
        if (!snap.Any())
        {
            ModelState.AddModelError("", "Usuario o contraseña incorrectos.");
            return View(vm);
        }

        var user = snap.Documents.First().ConvertTo<UserAccount>();
        if (!BCrypt.Net.BCrypt.Verify(vm.Password, user.PasswordHash))
        {
            ModelState.AddModelError("", "Usuario o contraseña incorrectos.");
            return View(vm);
        }

        var claims = new List<Claim>
        {
            //  Siempre usar el UID real si existe
            new Claim(ClaimTypes.NameIdentifier, user.GoogleUid ?? user.Id),

            new Claim("google_uid", user.GoogleUid ?? user.Id),

            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(new ClaimsPrincipal(identity));

        return RedirectToPage("/Recipes/Create");
    }

    [HttpGet] public IActionResult Register() => View(new RegisterViewModel());

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        // Evita duplicados por "usuario"
        var exists = await _db.Collection("usuarios")
                              .WhereEqualTo("usuario", vm.Username)
                              .Limit(1).GetSnapshotAsync();
        if (exists.Any())
        {
            ModelState.AddModelError(nameof(vm.Username), "El usuario ya existe.");
            return View(vm);
        }

        await _db.Collection("usuarios").AddAsync(new UserAccount
        {
            Username = vm.Username,   // se guardará como "usuario"
            Email = vm.Email,      // "correo"
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(vm.Password), // "hash"
            CreatedAt = Timestamp.FromDateTime(DateTime.UtcNow)      // "creadoEn"
        });

        return RedirectToAction(nameof(Login));
    }

    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }
    [HttpPost("LoginFirebase")]
    public async Task<IActionResult> LoginFirebase([FromBody] FirebaseLoginRequest request)
    {
        if (string.IsNullOrEmpty(request.Token))
            return BadRequest("Token no proporcionado.");

        try
        {
            var firebaseToken = await FirebaseAdmin.Auth.FirebaseAuth.DefaultInstance
                .VerifyIdTokenAsync(request.Token);

            var uidGoogle = firebaseToken.Uid;                // ✔ UID REAL
            var email = firebaseToken.Claims.TryGetValue("email", out var e) ? e.ToString() : null;

            // Buscar usuario por UID real
            var snap = await _db.Collection("usuarios")
                                .WhereEqualTo("google_uid", uidGoogle)
                                .Limit(1)
                                .GetSnapshotAsync();

            string name;

            if (!snap.Any())
            {
                // Crear usuario nuevo con UID real
                var doc = await _db.Collection("usuarios").AddAsync(new
                {
                    usuario = email?.Split('@')[0] ?? "usuario",
                    correo = email,
                    google_uid = uidGoogle,
                    creadoEn = Timestamp.GetCurrentTimestamp(),
                    estado = "activo",
                    rol = "usuario"
                });

                name = email?.Split('@')[0] ?? "usuario";
            }
            else
            {
                var docData = snap.Documents.First();
                name = docData.GetValue<string>("usuario");
            }

            // ✔ Crear sesión usando EL UID DE GOOGLE
            var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, uidGoogle), // 🔥 EL CORRECTO
            new Claim("google_uid", uidGoogle),
            new Claim(ClaimTypes.Name, name),
            new Claim("email", email ?? ""),
            new Claim(ClaimTypes.Role, "usuario")
        };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(new ClaimsPrincipal(identity));

            return new JsonResult(new { ok = true, uid = uidGoogle });
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

}
public class FirebaseLoginRequest
{
    public string Token { get; set; } = "";
}
