using CamCook.Models;
using CamCook.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CamCook.Pages.Recipes
{
    public class ListModel : PageModel
    {
        private readonly RecipeService _service;

        public List<Recipe> Recetas { get; set; }

        public ListModel(RecipeService service)
        {
            _service = service;
        }

        public void OnGet(string searchTerm)
        {
            Recetas = _service.GetRecetas(searchTerm);
        }
    }

}
