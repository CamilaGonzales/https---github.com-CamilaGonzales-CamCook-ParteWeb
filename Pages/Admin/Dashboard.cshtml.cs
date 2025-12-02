using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Globalization;

namespace CamCook.Pages.Admin
{
    [Authorize(Roles = "admin,administrador")]
    public class DashboardModel : PageModel
    {
        private readonly FirestoreDb _db;
        public DashboardModel(FirestoreDb db) => _db = db;

        // KPIs
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }

        public int TotalRecipes { get; set; }
        public int ApprovedRecipes { get; set; }
        public int RejectedRecipes { get; set; }
        public int PendingRecipes { get; set; }
        public int MarketplaceRecipes { get; set; }

        // Datos para el histograma
        public List<string> ChartLabels { get; set; } = new();
        public List<int> ChartMarketplaceByMonth { get; set; } = new();

        public async Task OnGet()
        {
            await LoadUsersAsync();
            await LoadRecipesAsync();
            await LoadMarketplaceHistogramAsync();
        }

        private async Task LoadUsersAsync()
        {
            var usersSnap = await _db.Collection("usuarios").GetSnapshotAsync();
            TotalUsers = usersSnap.Count;

            ActiveUsers = usersSnap.Documents.Count(d =>
                d.TryGetValue("estado", out string estado) &&
                string.Equals(estado, "activo", StringComparison.OrdinalIgnoreCase));
        }

        private async Task LoadRecipesAsync()
        {
            var snap = await _db.Collection("recetas").GetSnapshotAsync();
            TotalRecipes = snap.Count;

            foreach (var doc in snap.Documents)
            {
                var data = doc.ToDictionary();

                string estado = GetEstadoReceta(data);   // ?? usamos helper
                bool enMarketplace = IsEnMarketplace(data);

                // Normalizar y clasificar estados reales que usamos en Firestore.
                // En la base de datos es común encontrar valores como: "publicada", "pendiente_admin", "revisar", "rechazada_ai", etc.
                var s = (estado ?? string.Empty).ToLowerInvariant();

                if (s.Contains("public") || s.Contains("aprob"))
                {
                    ApprovedRecipes++;
                }
                else if (s.Contains("rechaz") || s.Contains("reprob") || s.Contains("reject"))
                {
                    RejectedRecipes++;
                }
                else if (s.Contains("pend") || s.Contains("revis") || s.Contains("review"))
                {
                    PendingRecipes++;
                }

                if (enMarketplace)
                    MarketplaceRecipes++;
            }
        }

        private async Task LoadMarketplaceHistogramAsync()
        {
            // Estadísticas: contar recetas creadas por mes (últimos 12 meses)
            var ahoraUtc = DateTime.UtcNow;
            var inicioUtc = new DateTime(ahoraUtc.Year, ahoraUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc)
                            .AddMonths(-11);

            // Consultamos por fecha de creación primaria: "creadoEn". Si no existe, intentamos campos alternativos.
            var query = _db.Collection("recetas")
                           .WhereGreaterThanOrEqualTo("creadoEn", Timestamp.FromDateTime(inicioUtc));

            var snap = await query.GetSnapshotAsync();

            var dict = new SortedDictionary<string, int>();
            for (int i = 0; i < 12; i++)
            {
                var mes = inicioUtc.AddMonths(i);
                var key = mes.ToString("yyyy-MM");
                dict[key] = 0;
            }

            foreach (var doc in snap.Documents)
            {
                var data = doc.ToDictionary();

                // Preferir creadoEn, luego publicadoEn/fechaPublicacion
                Timestamp? ts = null;
                if (data.TryGetValue("creadoEn", out var c) && c is Timestamp tc)
                    ts = tc;
                else if (data.TryGetValue("publicadoEn", out var p) && p is Timestamp tp)
                    ts = tp;
                else if (data.TryGetValue("fechaPublicacion", out var fp) && fp is Timestamp tfp)
                    ts = tfp;

                if (ts == null)
                    continue;

                var fecha = ts.Value.ToDateTime();
                var key = new DateTime(fecha.Year, fecha.Month, 1).ToString("yyyy-MM");

                if (dict.ContainsKey(key)) dict[key]++;
            }

            var cultura = new System.Globalization.CultureInfo("es-ES");
            ChartLabels = dict.Keys
                .Select(k =>
                {
                    var dt = DateTime.ParseExact(k + "-01", "yyyy-MM-dd", cultura);
                    return dt.ToString("MMM yy", cultura);
                })
                .ToList();

            // Reutilizamos la propiedad existente para mantener la vista sin cambios
            ChartMarketplaceByMonth = dict.Values.ToList();
        }

        private string GetEstadoReceta(IReadOnlyDictionary<string, object> data)
        {
            // 1) Campo plano: estado = "aprobada"
            if (data.TryGetValue("estado", out var estadoPlano) && estadoPlano != null)
                return estadoPlano.ToString()!.ToLower();

            // 2) Objeto anidado: estadoModeracion.estado o estadoModeracion.actual
            if (data.TryGetValue("estadoModeracion", out var estadoModObj) &&
                estadoModObj is Dictionary<string, object> estadoModDict)
            {
                if (estadoModDict.TryGetValue("estado", out var e) && e != null)
                    return e.ToString()!.ToLower();

                if (estadoModDict.TryGetValue("actual", out var a) && a != null)
                    return a.ToString()!.ToLower();
            }

            // 3) Otro nombre posible: status / statusModeracion
            if (data.TryGetValue("status", out var statusPlano) && statusPlano != null)
                return statusPlano.ToString()!.ToLower();

            return ""; // desconocido
        }

        private bool IsEnMarketplace(IReadOnlyDictionary<string, object> data)
        {
            // 1) Campo plano bool enMarketplace
            if (data.TryGetValue("enMarketplace", out var mk1) &&
                bool.TryParse(mk1?.ToString(), out var b1))
                return b1;

            // 2) Objeto marketplace.activo / marketplace.enabled
            if (data.TryGetValue("marketplace", out var mkObj) &&
                mkObj is Dictionary<string, object> mkDict)
            {
                if (mkDict.TryGetValue("activo", out var a) &&
                    bool.TryParse(a?.ToString(), out var b2))
                    return b2;

                if (mkDict.TryGetValue("enabled", out var e) &&
                    bool.TryParse(e?.ToString(), out var b3))
                    return b3;
            }

            return false;
        }

    }
}
