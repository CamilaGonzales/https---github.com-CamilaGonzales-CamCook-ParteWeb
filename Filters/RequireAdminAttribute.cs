using System.Security.Claims;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CamCook.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireAdminAttribute : Attribute, IAsyncPageFilter
{
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext ctx, PageHandlerExecutionDelegate next)
    {
        // 1) Debe estar autenticado
        var user = ctx.HttpContext.User;
        if (!(user?.Identity?.IsAuthenticated ?? false))
        {
            ctx.HttpContext.Response.Redirect("/Account/Login");
            return;
        }

        // 2) Obtener UID
        var uid = user.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? user.FindFirstValue("uid")
                  ?? string.Empty;

        if (string.IsNullOrWhiteSpace(uid))
        {
            ctx.HttpContext.Response.Redirect("/Account/Login");
            return;
        }

        // 3) Consultar Firestore: usuarios/{uid}
        var db = ctx.HttpContext.RequestServices.GetRequiredService<FirestoreDb>();
        var snap = await db.Collection("usuarios").Document(uid).GetSnapshotAsync();

        var isAdmin = false;
        if (snap.Exists)
        {
            var data = snap.ToDictionary();
            // Acepta cualquiera de estas marcas:
            if (data.TryGetValue("rol", out var rolObj) && rolObj?.ToString()?.Equals("admin", StringComparison.OrdinalIgnoreCase) == true)
                isAdmin = true;
            if (!isAdmin && data.TryGetValue("esAdmin", out var esAdminObj) && esAdminObj is bool b && b)
                isAdmin = true;
        }

        if (!isAdmin)
        {
            // Sin acceso → vuelve al inicio (o puedes devolver 403)
            ctx.HttpContext.Response.Redirect("/");
            return;
        }

        // 4) Adelante
        await next();
    }
}
