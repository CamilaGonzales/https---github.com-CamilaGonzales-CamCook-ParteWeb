using System.Security.Claims;
using CamCook.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CamCook.Pages.Admin.Moderacion;

[Authorize(Policy = "RequireAdmin")]
public class IndexModel : PageModel
{
    private readonly RecetaService _recetas;

    public IndexModel(RecetaService recetas) => _recetas = recetas;

    public List<RecetaPendienteDto> Pendientes { get; set; } = new();

    [TempData] public string? Msg { get; set; }
    [TempData] public string? Err { get; set; }

    public async Task OnGetAsync()
    {
        Pendientes = await _recetas.ObtenerPendientesAsync();
    }

    public async Task<IActionResult> OnPostApproveAsync(string id, string autorUid)
    {
        var adminUid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "admin";
        await _recetas.AprobarRecetaAsync(id, autorUid, adminUid);
        Msg = "Receta aprobada y autor ascendido a Chef.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(string id, string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
        {
            Err = "Debes indicar el motivo de rechazo.";
            return RedirectToPage();
        }
        var adminUid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "admin";
        await _recetas.RechazarRecetaAsync(id, motivo, adminUid);
        Msg = "Receta rechazada y motivo guardado.";
        return RedirectToPage();
    }
}
