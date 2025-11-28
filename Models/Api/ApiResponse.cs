namespace CamCook.Models.Api
{
    public class ApiResponse
    {
        public string query_original { get; set; }
        public int total_resultados { get; set; }
        public List<RecetaDto> resultados { get; set; }
    }
}
