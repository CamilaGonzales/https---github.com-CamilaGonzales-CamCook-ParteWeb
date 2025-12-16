using CamCook.Services;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CamCook.Pages.Account;

public class ForgotPasswordModel : PageModel
{
    private readonly FirestoreDb _db;
    private readonly IEmailService _email;

    public ForgotPasswordModel(FirestoreDb db, IEmailService email)
    {
        _db = db;
        _email = email;
    }

    [BindProperty]
    public string Email { get; set; } = "";

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        var email = (Email ?? "").Trim().ToLower();

        // Buscar usuario por correo
        var snap = await _db.Collection("usuarios")
            .WhereEqualTo("correo", email)
            .Limit(1)
            .GetSnapshotAsync();

        if (snap.Count == 0)
        {
            TempData["CorreoNoEncontrado"] = true;
            return Page();
        }

        var userDocId = snap.Documents[0].Id;

        // ? Token simple (solo letras y números)
        var token = Guid.NewGuid().ToString("N");

        // ? Guardar lo que ResetPassword valida
        await _db.Collection("passwordResets").AddAsync(new Dictionary<string, object>
        {
            { "correo", email },
            { "token", token },
            { "userDocId", userDocId },
            { "used", false },
            { "createdAt", Timestamp.FromDateTime(DateTime.UtcNow) },
            { "expiresAt", Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(15)) }
        });

        // Link
        var resetLink = Url.Page("/Account/ResetPassword", null, new { token = token }, Request.Scheme);

        // Email
        var subject = "Recuperar contraseña - CamCook";
        var body = $@"
            <div style='font-family:Arial'>
                <p>Haz clic aquí para cambiar tu contraseña:</p>
                <p><a href='{resetLink}'>Cambiar contraseña</a></p>
                <p><small>Este enlace expira en 15 minutos.</small></p>
            </div>
        ";

        await _email.SendAsync(email, subject, body);

        TempData["CorreoEnviado"] = true;
        return Page();
    }
}
