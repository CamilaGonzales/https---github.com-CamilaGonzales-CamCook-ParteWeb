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

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        [BindProperty(SupportsGet = true)]
        public int Page { get; set; } = 1;

        public int PageSize { get; } = 28;
        public int TotalItems { get; private set; }
        public int TotalPages { get; private set; }

        public string Saludo { get; set; } = "";
        public string NombreUsuario { get; set; } = "";

        // Para pasar todos los ids a Details (si lo necesitas)
        public string IdsConcatenados => string.Join(",", Recetas.Select(r => r.Id));

        public async Task OnGetAsync()
        {
            // Obtener UID y nombre del usuario
            var currentUid = User.FindFirstValue(ClaimTypes.NameIdentifier)
                           ?? User.FindFirstValue("uid");
            var userName = User.FindFirstValue(ClaimTypes.Name)
                         ?? User.FindFirstValue("email")
                         ?? "Visitante";

            NombreUsuario = userName;

            // Generar saludo según la hora del día
            var horaActual = DateTime.Now.Hour;
            if (horaActual >= 5 && horaActual < 12)
            {
                Saludo = "Buenos días";
            }
            else if (horaActual >= 12 && horaActual < 18)
            {
                Saludo = "Buenas tardes";
            }
            else
            {
                // Desde las 18:00 hasta antes de las 05:00 mostramos "Buenas noches"
                Saludo = "Buenas noches";
            }

            // Obtener todas (hasta un límite razonable) y paginar en el servidor
            var fetchLimit = Math.Max(Page * PageSize, PageSize);
            var all = await _svc.ObtenerPublicadasAsync(Search, fetchLimit);

            TotalItems = all.Count;
            TotalPages = Math.Max(1, (int)Math.Ceiling((double)TotalItems / PageSize));

            var pageItems = all
                .Skip((Math.Max(1, Page) - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            Recetas = pageItems
                .Select(d => _svc.MapToVm(d, currentUid))
                .ToList();
        }
    }
}
