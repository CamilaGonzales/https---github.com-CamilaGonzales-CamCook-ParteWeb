using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CamCook.Services;

namespace CamCook.Pages.Recipes
{
    public class DetailsModel : PageModel
    {
        private readonly RecetaService _svc;
        public DetailsModel(RecetaService svc) => _svc = svc;

        // Documento crudo (por si quieres depurar/inspeccionar)
        public IDictionary<string, object>? Doc { get; private set; }

        // Campos ya limpios para la vista
        public string? Title { get; private set; }
        public string? ImageUrl { get; private set; }
        public string? PrepTimeText { get; private set; }
        public int? Calories { get; private set; }
        public int? Servings { get; private set; }
        public string? Estado { get; private set; }
        public DateTime? PublicadoEn { get; private set; }
        public string? AuthorEmail { get; private set; }

        public string? Error { get; private set; }

        public List<IngredientVm> Ingredients { get; } = new();
        public List<StepVm> Steps { get; } = new();

        public async Task<IActionResult> OnGetAsync(string id, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                Error = "ID inválido.";
                return Page();
            }

            Doc = await _svc.ObtenerPorIdAsync(id, ct);
            if (Doc == null)
            {
                Error = "La receta no existe o fue eliminada.";
                return Page();
            }

            // --------- Lectura segura con fallback ----------
            Title = S(Doc, "titulo", "title");
            ImageUrl = S(Doc, "imagenUrl", "mainImageUrl");
            PrepTimeText = S(Doc, "tiempoPrep", "prepTimeText");
            AuthorEmail = S(Doc, "authorEmail", "autorEmail"); // por si alguna vez usaste otra clave
            Estado = S(Doc, "estado");

            Calories = I(Doc, "calorias", "calories");
            Servings = I(Doc, "porciones", "servings");

            // publicadoEn (Timestamp o DateTime) si existe
            PublicadoEn = T(Doc, "publicadoEn") ?? T(Doc, "creadoEn");

            // Ingredientes: 'ingredientes' (es) o 'ingredients' (camel)
            var ingredientesRaw = L(Doc, "ingredientes") ?? L(Doc, "ingredients");
            if (ingredientesRaw != null)
            {
                foreach (var row in ingredientesRaw)
                {
                    if (row is IDictionary<string, object> d)
                    {
                        // español
                        var n1 = S(d, "nombre");
                        var q1 = S(d, "cantidad");
                        var u1 = S(d, "unidad");

                        // camel
                        var n2 = S(d, "name");
                        var q2 = S(d, "quantity");
                        var u2 = S(d, "unit");

                        var name = FirstNonEmpty(n1, n2);
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            Ingredients.Add(new IngredientVm(
                                name,
                                FirstNonEmpty(q1, q2),
                                FirstNonEmpty(u1, u2)
                            ));
                        }
                    }
                }
            }

            // Pasos: 'pasos' (es) o 'steps' (camel)
            var pasosRaw = L(Doc, "pasos") ?? L(Doc, "steps");
            if (pasosRaw != null)
            {
                foreach (var row in pasosRaw)
                {
                    if (row is IDictionary<string, object> d)
                    {
                        // español
                        var descEs = S(d, "descripcion");
                        var imgEs = S(d, "imagenUrl");
                        var ordEs = I(d, "orden");

                        // camel
                        var descCa = S(d, "description");
                        var imgCa = S(d, "imageUrl");
                        var ordCa = I(d, "index");

                        var order = ordEs ?? ordCa ?? 0;
                        var desc = FirstNonEmpty(descEs, descCa);
                        var img = FirstNonEmpty(imgEs, imgCa);

                        Steps.Add(new StepVm(order, desc ?? "", img));
                    }
                }

                // Ordenar por 'orden'/'index'
                Steps.Sort((a, b) => a.Order.CompareTo(b.Order));
            }

            return Page();
        }

        // -------------------- Helpers --------------------

        private static string? FirstNonEmpty(params string?[] vals)
            => vals?.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));

        private static string S(IDictionary<string, object> d, params string[] keys)
        {
            foreach (var k in keys)
            {
                if (d.TryGetValue(k, out var v) && v != null)
                {
                    var s = v.ToString();
                    if (!string.IsNullOrWhiteSpace(s)) return s!;
                }
            }
            return string.Empty;
        }

        private static int? I(IDictionary<string, object> d, params string[] keys)
        {
            foreach (var k in keys)
            {
                if (d.TryGetValue(k, out var v) && v != null)
                {
                    // Firestore puede devolver long/int64
                    if (v is int i) return i;
                    if (v is long l) return (int)l;
                    if (int.TryParse(v.ToString(), out var p)) return p;
                }
            }
            return null;
        }

        private static DateTime? T(IDictionary<string, object> d, params string[] keys)
        {
            foreach (var k in keys)
            {
                if (d.TryGetValue(k, out var v) && v != null)
                {
                    if (v is Timestamp ts) return ts.ToDateTime();
                    if (v is DateTime dt) return dt;
                    if (DateTime.TryParse(v.ToString(), out var p)) return p;
                }
            }
            return null;
        }

        private static List<object>? L(IDictionary<string, object> d, params string[] keys)
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

        // ViewModels para la vista
        public record IngredientVm(string Name, string? Quantity, string? Unit);
        public record StepVm(int Order, string? Description, string? ImageUrl);
    }
}
