using CamCook.Services;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CamCook.Pages.Recipes
{
    [AllowAnonymous]
    public class DetailsModel : PageModel
    {
        private readonly RecetaService _svc;

        public DetailsModel(RecetaService svc)
        {
            _svc = svc;
        }

        // -------------------------------------------------------
        // Parámetros de entrada
        // -------------------------------------------------------
        [BindProperty(SupportsGet = true)]
        public string Id { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public string? Ids { get; set; } // lista de IDs concatenados por coma

        // -------------------------------------------------------
        // Resultado actual
        // -------------------------------------------------------
        public IDictionary<string, object>? Doc { get; private set; }
        public string? Title { get; private set; }
        public string? ImageUrl { get; private set; }
        public string? AuthorName { get; private set; }
        public string? AuthorUid { get; private set; }
        public string? AuthorEmail { get; private set; }

        public int? Calorias { get; private set; }
        public int? Porciones { get; private set; }
        public int? Likes { get; private set; }
        public int? Views { get; private set; }

        public DateTime? CreadoEn { get; private set; }
        public DateTime? ActualizadoEn { get; private set; }
        public DateTime? PublicadoEn { get; private set; }

        public string? Estado { get; private set; }
        public bool? NeedsModeration { get; private set; }
        public bool? Publicada { get; private set; }

        public IDictionary<string, object>? LikedBy { get; private set; }
        public IDictionary<string, object>? ViewedBy { get; private set; }
        public IDictionary<string, object>? Flags { get; private set; }
        public IDictionary<string, object>? AI { get; private set; }


        public List<IngredientVm> Ingredients { get; } = new();
        public List<StepVm> Steps { get; } = new();

        // -------------------------------------------------------
        // Prev / Next solo si hay IDs de búsqueda
        // -------------------------------------------------------
        public string? PrevId { get; private set; }
        public string? NextId { get; private set; }

        // Lista de IDs deserializada
        private List<string> IdsBusqueda { get; set; } = new();

        // -------------------------------------------------------
        // Lógica principal
        // -------------------------------------------------------
        public async Task<IActionResult> OnGetAsync(CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(Id))
                return NotFound("ID de receta inválido.");

            // -------------------------------------------------------
            // Si vienen IDs de búsqueda
            // -------------------------------------------------------
            if (!string.IsNullOrWhiteSpace(Ids))
                IdsBusqueda = Ids.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

            if (IdsBusqueda.Count > 0)
            {
                int index = IdsBusqueda.IndexOf(Id);
                if (index >= 0)
                {
                    PrevId = index > 0 ? IdsBusqueda[index - 1] : null;
                    NextId = index < IdsBusqueda.Count - 1 ? IdsBusqueda[index + 1] : null;
                }
            }

            // -------------------------------------------------------
            // Obtener receta actual desde Firestore
            // -------------------------------------------------------
            Doc = await _svc.ObtenerPorIdAsync(Id, ct);
            if (Doc == null)
                return NotFound("Receta no encontrada.");

            Title = FirstNonEmpty(Doc, "titulo", "title");
            ImageUrl = FirstNonEmpty(Doc, "imagenUrl", "mainImageUrl");
            AuthorName = FirstNonEmpty(Doc, "authorName");
            AuthorUid = FirstNonEmpty(Doc, "authorUid");
            AuthorEmail = FirstNonEmpty(Doc, "authorEmail");

            Calorias = IntFromDoc(Doc, "calorias");
            Porciones = IntFromDoc(Doc, "porciones");
            Likes = IntFromDoc(Doc, "likes");
            Views = IntFromDoc(Doc, "views");

            Estado = FirstNonEmpty(Doc, "estado");
            Publicada = Doc.ContainsKey("publicada") ? Doc["publicada"] as bool? : null;
            NeedsModeration = Doc.ContainsKey("needsModeration") ? Doc["needsModeration"] as bool? : null;

            // Timestamps
            CreadoEn = Doc["creadoEn"] as Timestamp? != null ? ((Timestamp)Doc["creadoEn"]).ToDateTime() : null;
            ActualizadoEn = Doc["actualizadoEn"] as Timestamp? != null ? ((Timestamp)Doc["actualizadoEn"]).ToDateTime() : null;
            PublicadoEn = Doc["publicadoEn"] as Timestamp? != null ? ((Timestamp)Doc["publicadoEn"]).ToDateTime() : null;

            // Maps
            LikedBy = Doc.TryGetValue("liked_by", out var lb) ? lb as IDictionary<string, object> : null;
            ViewedBy = Doc.TryGetValue("viewed_by", out var vb) ? vb as IDictionary<string, object> : null;
            Flags = Doc.TryGetValue("flags", out var fg) ? fg as IDictionary<string, object> : null;
            AI = Doc.TryGetValue("ai", out var ai) ? ai as IDictionary<string, object> : null;



            // Ingredientes
            var ingredientesRaw = ListFromDoc(Doc, "ingredientes") ?? ListFromDoc(Doc, "ingredients");
            if (ingredientesRaw != null)
            {
                foreach (var row in ingredientesRaw)
                {
                    if (row is IDictionary<string, object> d)
                    {
                        var name = FirstNonEmpty(d, "nombre", "name");
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            Ingredients.Add(new IngredientVm(
                                name,
                                FirstNonEmpty(d, "cantidad", "quantity"),
                                FirstNonEmpty(d, "unidad", "unit")
                            ));
                        }
                    }
                }
            }

            // Pasos
            var pasosRaw = ListFromDoc(Doc, "pasos") ?? ListFromDoc(Doc, "steps");
            if (pasosRaw != null)
            {
                foreach (var row in pasosRaw)
                {
                    if (row is IDictionary<string, object> d)
                    {
                        Steps.Add(new StepVm(
                            IntFromDoc(d, "orden", "index") ?? 0,
                            FirstNonEmpty(d, "descripcion", "description"),
                            FirstNonEmpty(d, "imagenUrl", "imageUrl")
                        ));
                    }
                }
                Steps.Sort((a, b) => a.Order.CompareTo(b.Order));
            }

            return Page();
        }

        // -------------------------------------------------------
        // Helpers
        // -------------------------------------------------------
        private static string? FirstNonEmpty(IDictionary<string, object> d, params string[] keys)
        {
            foreach (var k in keys)
            {
                if (d.TryGetValue(k, out var v) && v != null)
                {
                    var s = v.ToString();
                    if (!string.IsNullOrWhiteSpace(s)) return s!;
                }
            }
            return null;
        }

        private static List<object>? ListFromDoc(IDictionary<string, object> d, params string[] keys)
        {
            foreach (var k in keys)
            {
                if (d.TryGetValue(k, out var v) && v is IEnumerable e)
                {
                    var list = new List<object>();
                    foreach (var it in e) list.Add(it!);
                    return list;
                }
            }
            return null;
        }

        private static int? IntFromDoc(IDictionary<string, object> d, params string[] keys)
        {
            foreach (var k in keys)
            {
                if (d.TryGetValue(k, out var v) && v != null)
                {
                    if (v is int i) return i;
                    if (v is long l) return (int)l;
                    if (int.TryParse(v.ToString(), out var p)) return p;
                }
            }
            return null;
        }

        public record IngredientVm(string Name, string? Quantity, string? Unit);
        public record StepVm(int Order, string? Description, string? ImageUrl);
    }
}
