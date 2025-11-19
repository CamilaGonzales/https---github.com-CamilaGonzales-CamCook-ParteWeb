using CamCook.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using System.Threading;


namespace CamCook.Pages.Recipes;

[AllowAnonymous]
public class ListModel : PageModel
{
    private readonly RecetaService _svc;
    public ListModel(RecetaService svc) => _svc = svc;

    public List<Dictionary<string, object>> Recetas { get; set; } = new();

    public async Task OnGetAsync()
    {
        string? uid = User?.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User?.FindFirstValue("uid");
        Recetas = await _svc.ObtenerPublicadasAsync();
        Recetas = await _svc.ObtenerPublicadasYBorradoresAsync(uid);
    }

}
