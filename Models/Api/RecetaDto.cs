namespace CamCook.Models.Api
{
    public class RecetaDto
    {
        public string id { get; set; }
        public string titulo { get; set; }
        public string descripcion { get; set; }
        public string imagenUrl { get; set; }
        public int? calorias { get; set; }
        public int porciones { get; set; }
        public string tiempoFormateado { get; set; }

        public List<IngredienteDto> ingredientes { get; set; }
        public List<PasoDto> pasos { get; set; }
    }

    public class IngredienteDto
    {
        public string nombre { get; set; }
        public string cantidad { get; set; }
        public string unidad { get; set; }
    }
}
