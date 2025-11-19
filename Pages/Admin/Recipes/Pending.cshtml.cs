using CamCook.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CamCook.Pages.Admin.Recipes
{
    public class PendingModel : PageModel
    {
        private readonly RecetaService _svc;
        public List<RecetaPendienteDto> Pendientes { get; private set; } = new();

        public PendingModel(RecetaService svc) => _svc = svc;

        public async Task OnGet()
        {
            Pendientes = await _svc.ObtenerPendientesAsync();
        }

        public async Task OnPostAprobarAsync(string id, string? autorUid)
        {
            await _svc.AprobarRecetaAsync(id, autorUid);
            Response.Redirect(Request.Path);
        }

        public async Task OnPostRechazarAsync(string id, string motivo)
        {
            await _svc.RechazarRecetaAsync(id, motivo);
            Response.Redirect(Request.Path);
        }
    }
}
