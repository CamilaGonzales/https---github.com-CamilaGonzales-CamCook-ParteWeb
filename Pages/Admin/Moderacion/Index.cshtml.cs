using CamCook.Filters;
using CamCook.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;

namespace CamCook.Pages.Admin.Moderacion;

[RequireAdmin]
public class IndexModel : PageModel
{
    private readonly RecetaService _svc;
    private readonly UsuarioService _usuarios; // nuevo para leer roles en la vista (abajo)
    public IndexModel(RecetaService svc, UsuarioService usuarios)
    {
        _svc = svc;
        _usuarios = usuarios;
    }

    public List<RecetaPendienteDto> Pendientes { get; set; } = new();
    public Dictionary<string, List<string>> RolesPorAutor { get; set; } = new();

    [TempData] public string? Msg { get; set; }
    [TempData] public string? Err { get; set; }

    public async Task OnGetAsync()
    {
        Pendientes = await _svc.ObtenerPendientesAsync();

        // Obtener los autorUid únicos de las recetas listadas
        var autorUids = Pendientes
            .Select(p => p.Data.TryGetValue("autorUid", out var a) ? a?.ToString() : null)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct()!
            .ToList();

        // Cargar sus roles para mostrarlos en la barrita
        RolesPorAutor = await _usuarios.ObtenerRolesAsync(autorUids);
    }

    public async Task<IActionResult> OnPostAprobarAsync(string id, string autorUid)
    {
        await _svc.AprobarRecetaAsync(id, autorUid);
        TempData["Msg"] = "Receta aprobada y publicada. Roles actualizados.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRechazarAsync(string id, string motivo)
    {
        await _svc.RechazarRecetaAsync(id, motivo);
        TempData["Msg"] = "Receta rechazada.";
        return RedirectToPage();
    }
}
