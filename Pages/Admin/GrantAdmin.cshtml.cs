using FirebaseAdmin.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CamCook.Pages.AdminTools;

[AllowAnonymous]
public class GrantAdminModel : PageModel
{
    private readonly IConfiguration _cfg;
    public GrantAdminModel(IConfiguration cfg) => _cfg = cfg;

    public string Message { get; private set; } = "";

    public async Task OnGet(string? email, string? uid, string? key, bool revoke = false)
    {
        var expected = _cfg["AdminGrant:Key"];
        if (string.IsNullOrWhiteSpace(expected) || key != expected)
        {
            Message = "?? Clave inválida. Configura AdminGrant:Key y pásala como ?key=...";
            return;
        }

        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(uid))
        {
            Message = "Usa ?email=tu@correo.com o ?uid=EL_UID y ?key=LA_CLAVE";
            return;
        }

        UserRecord user;
        try
        {
            user = !string.IsNullOrWhiteSpace(email)
                ? await FirebaseAuth.DefaultInstance.GetUserByEmailAsync(email!)
                : await FirebaseAuth.DefaultInstance.GetUserAsync(uid!);
        }
        catch
        {
            Message = "Usuario no encontrado.";
            return;
        }

        var claims = new Dictionary<string, object>(
     user.CustomClaims ?? new Dictionary<string, object>());

        if (revoke)
        {
            claims.Remove("admin");
            await FirebaseAuth.DefaultInstance.SetCustomUserClaimsAsync(user.Uid, claims);
            Message = $"? {user.Email} ya no es admin.";
            return;
        }

        claims["admin"] = true;
        await FirebaseAuth.DefaultInstance.SetCustomUserClaimsAsync(user.Uid, claims);
        Message = $"? {user.Email} ahora es ADMIN. Cierra sesión y vuelve a entrar para aplicar.";
    }
}
