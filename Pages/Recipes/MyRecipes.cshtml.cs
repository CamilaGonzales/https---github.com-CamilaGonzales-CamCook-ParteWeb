using System.Security.Claims;
using CamCook.Models;
using CamCook.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;

namespace CamCook.Pages.Recipes
{
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class MyRecipesModel : PageModel
    {
        private readonly RecetaService _svc;

        public MyRecipesModel(RecetaService svc)
        {
            _svc = svc;
        }

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Estado { get; set; } = ""; // "", "borrador", o null para ambas

        [BindProperty(SupportsGet = true)]
        public int Page { get; set; } = 1;

        public int PageSize { get; } = 28;
        public int TotalItems { get; private set; }
        public int TotalPages { get; private set; }

        public List<RecipieViewModel> Recetas { get; set; } = new();

        public async Task OnGetAsync()
        {
            // UID del usuario logueado
            var currentUid = User.FindFirstValue(ClaimTypes.NameIdentifier)
                           ?? User.FindFirstValue("uid");

            if (string.IsNullOrWhiteSpace(currentUid))
            {
                Recetas = new();
                return;
            }

            var data = new List<Dictionary<string, object>>();

            // Si Estado == "borrador", obtener solo borradores del usuario
            if (Estado == "borrador")
            {
                data = await _svc.ObtenerPorAutorYEstadoAsync(currentUid, "borrador");
            }
            // Si Estado == "" o null, obtener publicadas + borradores del usuario
            else
            {
                // Recetas publicadas del usuario
                var publicadas = await _svc.ObtenerPorAutorYEstadoAsync(currentUid, "publicada");
                
                // Recetas borradores del usuario
                var borradores = await _svc.ObtenerPorAutorYEstadoAsync(currentUid, "borrador");
                
                data = publicadas.Concat(borradores).ToList();
            }

            // Aplicar búsqueda si está activada
            if (!string.IsNullOrWhiteSpace(Search))
            {
                var s = Search.Trim().ToLower();
                data = data
                    .Where(r =>
                        (r.TryGetValue("titulo", out var t) && t?.ToString()?.ToLower().Contains(s) == true) ||
                        (r.TryGetValue("descripcion", out var d) && d?.ToString()?.ToLower().Contains(s) == true)
                    )
                    .ToList();
            }

            var lista = new List<RecipieViewModel>();

            // Paginación: calcular totales y seleccionar la página
            TotalItems = data.Count;
            TotalPages = Math.Max(1, (int)Math.Ceiling((double)TotalItems / PageSize));

            var pageItems = data
                .Skip((Math.Max(1, Page) - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            foreach (var dict in pageItems)
            {
                var vm = _svc.MapToVm(dict, currentUid);

                // Por si acaso, reforzamos EsAutor
                vm.EsAutor = !string.IsNullOrWhiteSpace(vm.AutorUid) &&
                             vm.AutorUid == currentUid;

                lista.Add(vm);
            }

            Recetas = lista;
        }
    }
}
