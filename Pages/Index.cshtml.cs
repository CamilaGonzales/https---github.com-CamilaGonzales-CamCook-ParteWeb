using CamCook.Models;
using CamCook.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace CamCook.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(ILogger<IndexModel> logger, RecetaService svc)
        {
            _logger = logger;
            _svc = svc;
        }
        private readonly RecetaService _svc;

        public List<RecipieViewModel> Recetas { get; set; } = new();

        // Para pasar todos los ids a Details (si lo necesitas)
        public string IdsConcatenados => string.Join(",", Recetas.Select(r => r.Id));

        public async Task OnGetAsync()
        {
            // no necesitamos EsAutor aquí, pero igual puedes pasarlo si quieres
            var currentUid = User.FindFirstValue(ClaimTypes.NameIdentifier)
                           ?? User.FindFirstValue("uid");

            var data = await _svc.ObtenerPublicadasAsync(null);
            Recetas = data
                .Select(d => _svc.MapToVm(d, currentUid))   // o MapToVm(d) si prefieres
                .ToList();
        }
    }
}
