using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CamCook.Models;
using Google.Cloud.Firestore;

namespace CamCook.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly FirestoreDb _db;
        public RegisterModel(FirestoreDb db) => _db = db;

        [BindProperty]
        public RegisterViewModel Input { get; set; } = new();

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            // Validaciones básicas
            if (!ModelState.IsValid) return Page();

            // Confirmación de contraseña
            if (!string.Equals(Input.Password, Input.ConfirmPassword))
            {
                ModelState.AddModelError(nameof(Input.ConfirmPassword), "Las contraseñas no coinciden.");
                return Page();
            }

            // Validación de seguridad de la contraseña
            var password = Input.Password;

            if (password.Length < 8 ||
                !password.Any(char.IsLower) ||
                !password.Any(char.IsUpper) ||
                !password.Any(char.IsDigit))
            {
                ModelState.AddModelError(nameof(Input.Password),
                    "La contraseña debe tener al menos 8 caracteres, incluir mayúsculas, minúsculas y números.");
                return Page();
            }

            // Validación de nombre de usuario único
            var existsUser = await _db.Collection("usuarios")
                .WhereEqualTo("usuario", Input.Username)
                .Limit(1)
                .GetSnapshotAsync();

            if (existsUser.Any())
            {
                ModelState.AddModelError(nameof(Input.Username), "El usuario ya existe.");
                return Page();
            }

            // Validación de correo único
            var existsEmail = await _db.Collection("usuarios")
                .WhereEqualTo("correo", Input.Email)
                .Limit(1)
                .GetSnapshotAsync();

            if (existsEmail.Any())
            {
                ModelState.AddModelError(nameof(Input.Email), "Este correo ya está registrado.");
                return Page();
            }

            // Crear documento en Firestore
            var docRef = _db.Collection("usuarios").Document();

            // Hash seguro con BCrypt
            var hash = BCrypt.Net.BCrypt.HashPassword(Input.Password, workFactor: 10);

            var data = new Dictionary<string, object>
            {
                ["usuario"] = Input.Username,
                ["correo"] = Input.Email,
                ["contraseña"] = hash,
                ["rol"] = "usuario",
                ["roles"] = new[] { "usuario" },
                ["creadoEn"] = Timestamp.FromDateTime(DateTime.UtcNow),
                ["actualizadoEn"] = Timestamp.FromDateTime(DateTime.UtcNow),
                ["estado"] = "activo"
            };

            await docRef.SetAsync(data);

            TempData["ok"] = "Registro exitoso. Ahora inicia sesión.";
            return RedirectToPage("/Account/Login");
        }
    }
}
