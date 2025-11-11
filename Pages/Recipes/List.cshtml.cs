using CamCook.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Threading;


namespace CamCook.Pages.Recipes;

public class ListModel : PageModel
{
    private readonly RecetaService _svc;
    public ListModel(RecetaService svc) => _svc = svc;

    public List<Dictionary<string, object>> Recetas { get; set; } = new();

    public async Task OnGetAsync()
    {
        Recetas = await _svc.ObtenerPublicadasAsync();
    }

}
