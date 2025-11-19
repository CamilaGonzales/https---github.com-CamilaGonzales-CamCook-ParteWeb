using CamCook.Services;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
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
        public DetailsModel(RecetaService svc) => _svc = svc;

        public IDictionary<string, object>? Doc { get; private set; }

        public string? Title { get; private set; }
        public string? ImageUrl { get; private set; }
        public string? PrepTimeText { get; private set; }
        public int? Calories { get; private set; }
        public int? Servings { get; private set; }
        public string? Estado { get; private set; }
        public DateTime? PublicadoEn { get; private set; }

        // ?? NUEVO: nombre del autor
        public string? AuthorName { get; private set; }
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

            Title = S(Doc, "titulo", "title");
            ImageUrl = S(Doc, "imagenUrl", "mainImageUrl");
            PrepTimeText = S(Doc, "tiempoPrep", "prepTimeText");

            // ?? LECTURA DEL AUTOR
            AuthorName = S(Doc, "authorName", "autorNombre");
            AuthorEmail = S(Doc, "authorEmail", "autorEmail");

            Estado = S(Doc, "estado");
            Calories = I(Doc, "calorias", "calories");
            Servings = I(Doc, "porciones", "servings");

            PublicadoEn = T(Doc, "publicadoEn") ?? T(Doc, "creadoEn");

            var ingredientesRaw = L(Doc, "ingredientes") ?? L(Doc, "ingredients");
            if (ingredientesRaw != null)
            {
                foreach (var row in ingredientesRaw)
                {
                    if (row is IDictionary<string, object> d)
                    {
                        var name = FirstNonEmpty(
                            S(d, "nombre"), S(d, "name")
                        );

                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            Ingredients.Add(new IngredientVm(
                                name,
                                FirstNonEmpty(S(d, "cantidad"), S(d, "quantity")),
                                FirstNonEmpty(S(d, "unidad"), S(d, "unit"))
                            ));
                        }
                    }
                }
            }

            var pasosRaw = L(Doc, "pasos") ?? L(Doc, "steps");
            if (pasosRaw != null)
            {
                foreach (var row in pasosRaw)
                {
                    if (row is IDictionary<string, object> d)
                    {
                        Steps.Add(new StepVm(
                            I(d, "orden", "index") ?? 0,
                            FirstNonEmpty(S(d, "descripcion"), S(d, "description")),
                            FirstNonEmpty(S(d, "imagenUrl"), S(d, "imageUrl"))
                        ));
                    }
                }

                Steps.Sort((a, b) => a.Order.CompareTo(b.Order));
            }

            return Page();
        }

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

        public record IngredientVm(string Name, string? Quantity, string? Unit);
        public record StepVm(int Order, string? Description, string? ImageUrl);
    }
}
