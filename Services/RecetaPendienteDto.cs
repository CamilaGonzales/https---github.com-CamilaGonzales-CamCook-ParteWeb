namespace CamCook.Services;

public class RecetaPendienteDto
{
    public string Id { get; set; } = "";
    public Dictionary<string, object> Data { get; set; } = new();
    public string? Estado { get; set; } // opcional si quieres mapear directo
}
