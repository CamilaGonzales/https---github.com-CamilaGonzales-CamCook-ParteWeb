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
            if (!ModelState.IsValid) return Page();

            var exists = await _db.Collection("usuarios")
                .WhereEqualTo("usuario", Input.Username)
                .Limit(1).GetSnapshotAsync();

            if (exists.Any())
            {
                ModelState.AddModelError(nameof(Input.Username), "El usuario ya existe.");
                return Page();
            }

            var docRef = _db.Collection("usuarios").Document();
            var user = new User
            {
                Username = Input.Username,
                Email = Input.Email,
                Password = BCrypt.Net.BCrypt.HashPassword(Input.Password),
                CreatedAt = Timestamp.FromDateTime(DateTime.UtcNow)
            };

            await docRef.SetAsync(user);
            TempData["ok"] = "Registro exitoso. Ahora inicia sesión.";
            return RedirectToPage("/Account/Login");
        }
    }
}
