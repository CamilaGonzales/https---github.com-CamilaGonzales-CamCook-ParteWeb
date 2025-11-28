namespace CamCook.Models
{
    public class RecipieViewModel
    {
        public string Id { get; set; }
        public string Titulo { get; set; }
        public string Descripcion { get; set; }
        public string ImagenUrl { get; set; }
        public bool Publicado { get; set; }

        public int? Calorias { get; set; }
        public int? Likes { get; set; }
        public int? Views { get; set; }

        public string? Autor { get; set; }
        public string? AutorUid { get; set; }

        public bool EsAutor { get; set; }
    }
}
