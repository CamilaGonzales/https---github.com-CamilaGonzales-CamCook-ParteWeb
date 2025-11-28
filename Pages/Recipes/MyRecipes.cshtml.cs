using System.Security.Claims;
using CamCook.Models;
using CamCook.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;

namespace CamCook.Pages.Recipes
{
    public class MyRecipesModel : PageModel
    {
        private readonly RecetaService _svc;

        public MyRecipesModel(RecetaService svc)
        {
            _svc = svc;
        }

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

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

            // ?? SOLO recetas de ESTE autor
            var data = await _svc.ObtenerDeAutorAsync(currentUid, Search);

            var lista = new List<RecipieViewModel>();

            foreach (var dict in data)
            {
                var vm = _svc.MapToVm(dict, currentUid);

                // Por si acaso, reforzamos EsAutor aquí
                vm.EsAutor = !string.IsNullOrWhiteSpace(vm.AutorUid) &&
                             vm.AutorUid == currentUid;

                lista.Add(vm);
            }

            Recetas = lista;
        }
    }
}
