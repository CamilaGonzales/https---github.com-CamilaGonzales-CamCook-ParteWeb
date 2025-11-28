namespace CamCook.Models.Api
{
    public class IdsResponse
    {
        public string query_original { get; set; }
        public int total_resultados { get; set; }
        public List<string> resultados { get; set; }
        public List<string> ids { get; set; } = new();

    }
}
