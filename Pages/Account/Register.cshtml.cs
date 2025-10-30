using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CamCook.Models;
using CamCook.Services.Security; // <-- NUEVO
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
            // Validaciones básicas del modelo
            if (!ModelState.IsValid) return Page();

            // Confirmación de contraseña
            if (!string.Equals(Input.Password, Input.ConfirmPassword))
            {
                ModelState.AddModelError(nameof(Input.ConfirmPassword), "Las contraseñas no coinciden.");
                return Page();
            }

            // Validación de contraseña (servidor)
            if (!PasswordValidator.Validate(Input.Password, Input.Email, Input.Username, out var errors))
            {
                foreach (var e in errors)
                    ModelState.AddModelError(nameof(Input.Password), e);
                return Page();
            }

            // Unicidad de username
            var existsUser = await _db.Collection("usuarios")
                .WhereEqualTo("usuario", Input.Username)
                .Limit(1).GetSnapshotAsync();

            if (existsUser.Any())
            {
                ModelState.AddModelError(nameof(Input.Username), "El usuario ya existe.");
                return Page();
            }

            // (Opcional) Unicidad de email
            var existsEmail = await _db.Collection("usuarios")
                .WhereEqualTo("correo", Input.Email)
                .Limit(1).GetSnapshotAsync();

            if (existsEmail.Any())
            {
                ModelState.AddModelError(nameof(Input.Email), "Este correo ya está registrado.");
                return Page();
            }

            // Crear documento
            var docRef = _db.Collection("usuarios").Document();

            // Hash con BCrypt (ajusta workFactor si deseas 10-12)
            var hash = BCrypt.Net.BCrypt.HashPassword(Input.Password, workFactor: 11);

            // Si tu clase User NO tiene campos 'rol' ni 'roles', guardamos con diccionario;
            // así no rompes tu modelo existente.
            var data = new Dictionary<string, object>
            {
                ["usuario"] = Input.Username,
                ["correo"] = Input.Email,
                ["contraseña"] = hash, // si en tu modelo usas 'Password', ajusta el nombre del campo
                ["rol"] = "usuario",   // legado
                ["roles"] = new[] { "usuario" }, // nuevo esquema múltiple
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
