using CamCook.Models;
using CamCook.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace CamCook.Pages
{
    [Authorize]
    public class FavoritosModel : PageModel
    {
        private readonly RecetaService _svc;

        public FavoritosModel(RecetaService svc)
        {
            _svc = svc;
        }

        [BindProperty(SupportsGet = true)]
        public int Page { get; set; } = 1;

        public int PageSize { get; } = 28;
        public int TotalItems { get; private set; }
        public int TotalPages { get; private set; }

        public List<RecipieViewModel> Favoritos { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(CancellationToken ct)
        {
            // UID del usuario logueado
            var currentUid = User.FindFirstValue(ClaimTypes.NameIdentifier)
                           ?? User.FindFirstValue("uid");

            if (string.IsNullOrWhiteSpace(currentUid))
            {
                return RedirectToPage("/Account/Login");
            }

            // Obtener todos los favoritos del usuario
            var data = await _svc.ObtenerFavoritosAsync(currentUid, ct);

            // Paginación
            TotalItems = data.Count;
            TotalPages = Math.Max(1, (int)Math.Ceiling((double)TotalItems / PageSize));

            var pageItems = data
                .Skip((Math.Max(1, Page) - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            // Mapear a ViewModels
            var lista = new List<RecipieViewModel>();
            foreach (var dict in pageItems)
            {
                var vm = _svc.MapToVm(dict, currentUid);
                lista.Add(vm);
            }

            Favoritos = lista;
            return Page();
        }
    }
}
