using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CamCook.Pages.Account;

public class ResetPasswordModel : PageModel
{
    private readonly FirestoreDb _db;

    public ResetPasswordModel(FirestoreDb db)
    {
        _db = db;
    }

    [BindProperty(SupportsGet = true)]
    public string Token { get; set; } = "";

    [BindProperty]
    public string NewPassword { get; set; } = "";

    [BindProperty]
    public string ConfirmPassword { get; set; } = "";

    public bool TokenValid { get; set; } = false;
    public string Error { get; set; } = "";
    public string Success { get; set; } = "";

    public async Task OnGetAsync()
    {
        TokenValid = await IsTokenValid(Token);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Error = "";
        Success = "";

        var resetDoc = await GetResetDocByToken(Token);
        if (resetDoc == null)
        {
            TokenValid = false;
            Error = "Enlace inválido o expirado.";
            return Page();
        }

        var data = resetDoc.ToDictionary();

        // used
        var used = data.TryGetValue("used", out var usedObj) && usedObj is bool b && b;

        // expiresAt
        if (!data.TryGetValue("expiresAt", out var expObj) || expObj is not Timestamp expiresAt)
        {
            TokenValid = false;
            Error = "Enlace inválido o expirado.";
            return Page();
        }

        if (used || expiresAt.ToDateTime() < DateTime.UtcNow)
        {
            TokenValid = false;
            Error = "Enlace inválido o expirado.";
            return Page();
        }

        TokenValid = true;

        // validar pass
        if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 6)
        {
            Error = "La contraseña debe tener al menos 6 caracteres.";
            return Page();
        }

        if (NewPassword != ConfirmPassword)
        {
            Error = "Las contraseñas no coinciden.";
            return Page();
        }

        // userDocId
        if (!data.TryGetValue("userDocId", out var idObj) || idObj == null)
        {
            TokenValid = false;
            Error = "No se pudo asociar el usuario.";
            return Page();
        }

        var userDocId = idObj.ToString();

        // hash
        var hashed = BCrypt.Net.BCrypt.HashPassword(NewPassword);

        // update user
        await _db.Collection("usuarios").Document(userDocId)
            .UpdateAsync(new Dictionary<string, object>
            {
                { "contraseña", hashed },
                { "actualizadoEn", Timestamp.FromDateTime(DateTime.UtcNow) }
            });

        // mark token used
        await resetDoc.Reference.UpdateAsync(new Dictionary<string, object>
        {
            { "used", true },
            { "usedAt", Timestamp.FromDateTime(DateTime.UtcNow) }
        });

        Success = "Contraseña actualizada correctamente.";
        return Page();
    }

    private async Task<bool> IsTokenValid(string token)
    {
        var doc = await GetResetDocByToken(token);
        if (doc == null) return false;

        var data = doc.ToDictionary();

        var used = data.TryGetValue("used", out var usedObj) && usedObj is bool b && b;
        if (used) return false;

        if (!data.TryGetValue("expiresAt", out var expObj) || expObj is not Timestamp expiresAt)
            return false;

        return expiresAt.ToDateTime() >= DateTime.UtcNow;
    }

    private async Task<DocumentSnapshot?> GetResetDocByToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        var snap = await _db.Collection("passwordResets")
            .WhereEqualTo("token", token)
            .Limit(1)
            .GetSnapshotAsync();

        return snap.Count == 0 ? null : snap.Documents[0];
    }
}
