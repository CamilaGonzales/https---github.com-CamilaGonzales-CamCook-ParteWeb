using CamCook.Models;
using CamCook.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CamCook.Models.Api;
using System.Security.Claims;

namespace CamCook.Pages.Recipes;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class ListModel : PageModel
{
    private readonly RecetaService _svc;
    private readonly HttpClient _http;
    public int? Calorias { get; set; }
    public int? Likes { get; set; }
    public int? Views { get; set; }
    public string? Autor { get; set; }
    public string? AutorUid { get; set; }

    public ListModel(RecetaService svc, IHttpClientFactory factory)
    {
        _svc = svc;
        _http = factory.CreateClient();
    }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    public List<RecipieViewModel> Recetas { get; set; } = new();
    public List<RecipieViewModel> Borradores { get; set; } = new();

    public async Task OnGetAsync()
    {
        var currentUid = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("uid");

        if (!string.IsNullOrWhiteSpace(Search))
        {
            string url = $"https://fastapireconocimiento-2.onrender.com/buscar_ids?query={Uri.EscapeDataString(Search)}";

            IdsResponse? idsResp;
            try
            {
                idsResp = await _http.GetFromJsonAsync<IdsResponse>(url);
            }
            catch
            {
                Recetas = new();
                return;
            }

            if (idsResp == null || idsResp.ids == null || idsResp.ids.Count == 0)
            {
                Recetas = new();
                return;
            }

            // Guardamos los IDs en TempData para ocultarlos en la URL
            TempData["IdsBusqueda"] = string.Join(",", idsResp.ids);

            var resultado = new List<RecipieViewModel>();
            foreach (var id in idsResp.ids)
            {
                var recetaFire = await _svc.ObtenerPorIdAsync(id);
                if (recetaFire == null) continue;

                string? imagen = recetaFire.mainImageUrl ?? recetaFire.imagenUrl;
                if (string.IsNullOrWhiteSpace(imagen) && recetaFire.Steps != null)
                {
                    imagen = recetaFire.Steps
                        .Where(s => !string.IsNullOrWhiteSpace(s.ImageUrl))
                        .Select(s => s.ImageUrl)
                        .FirstOrDefault();
                }

                resultado.Add(new RecipieViewModel
                {
                    Id = recetaFire.Id,
                    Titulo = recetaFire.Title,
                    Descripcion = recetaFire.Steps?.FirstOrDefault()?.Description ?? "",
                    ImagenUrl = imagen,
                    Autor = recetaFire.AuthorEmail ?? "",
                    AutorUid = recetaFire.AuthorUid,
                    Calorias = recetaFire.Calories,
                    Likes = recetaFire.Likes,
                    Views = recetaFire.Views,
                    Publicado = true,
                    EsAutor = !string.IsNullOrWhiteSpace(currentUid) &&
                          !string.IsNullOrWhiteSpace(AutorUid) &&
                          AutorUid == currentUid
                });
            }

            Recetas = resultado;
            return;
        }

        // 1) Publicadas (como ya lo haces)
        var publicadas = await _svc.ObtenerPublicadasAsync(null);
        Recetas = publicadas
            .Select(x => _svc.MapToVm(x, currentUid))
            .ToList();

        // 2) Borradores del usuario actual (aqu� debes usar un m�todo de servicio)
        if (!string.IsNullOrWhiteSpace(currentUid))
        {
            var borradores = await _svc.ObtenerPorAutorYEstadoAsync(currentUid, "borrador");
            Borradores = borradores
                .Select(x => _svc.MapToVm(x, currentUid))
                .ToList();
        }
    }
}
