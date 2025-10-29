using Google.Cloud.Firestore;
using FirebaseAdmin.Auth;

namespace CamCook.Services;

public class RecetaPendienteDto
{
    public string Id { get; set; } = default!;
    public Dictionary<string, object> Data { get; set; } = default!;
}

public class RecetaService
{
    private readonly FirestoreDb _db;

    public RecetaService(IConfiguration config)
    {
        var projectId = config["Firebase:ProjectId"]
            ?? throw new InvalidOperationException("Falta Firebase:ProjectId");
        _db = FirestoreDb.Create(projectId);
    }

    public async Task<List<RecetaPendienteDto>> ObtenerPendientesAsync()
    {
        var snapshot = await _db.Collection("recetas")
            .WhereEqualTo("estado", "Pendiente")
            .GetSnapshotAsync();

        return snapshot.Documents
            .Select(d => new RecetaPendienteDto { Id = d.Id, Data = d.ToDictionary() })
            .ToList();
    }

    public async Task AprobarRecetaAsync(string idReceta, string uidAutor, string adminUid)
    {
        var doc = _db.Collection("recetas").Document(idReceta);
        await doc.UpdateAsync(new Dictionary<string, object>
        {
            { "estado", "Aprobado" },
            { "moderadoPor", adminUid },
            { "fechaAprobacion", Timestamp.GetCurrentTimestamp() },
            { "motivoRechazo", FieldValue.Delete }
        });

        var user = await FirebaseAuth.DefaultInstance.GetUserAsync(uidAutor);
        var claims = new Dictionary<string, object>(user.CustomClaims ?? new Dictionary<string, object>());
        claims["chef"] = true;

        await FirebaseAuth.DefaultInstance.SetCustomUserClaimsAsync(uidAutor, claims);

    }

    public async Task RechazarRecetaAsync(string idReceta, string motivo, string adminUid)
    {
        var doc = _db.Collection("recetas").Document(idReceta);
        await doc.UpdateAsync(new Dictionary<string, object>
        {
            { "estado", "Rechazado" },
            { "motivoRechazo", motivo },
            { "moderadoPor", adminUid },
            { "fechaRechazo", Timestamp.GetCurrentTimestamp() }
        });
    }
}
