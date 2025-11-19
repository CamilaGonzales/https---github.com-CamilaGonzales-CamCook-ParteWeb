using Google.Cloud.Firestore;

namespace CamCook.Models;

[FirestoreData]
public class UserAccount
{
    [FirestoreDocumentId] public string Id { get; set; } = default!;

    // Campos en tu documento Firestore:
    [FirestoreProperty("usuario")] public string Username { get; set; } = "";
    [FirestoreProperty("correo")] public string Email { get; set; } = "";
    [FirestoreProperty("contraseña")] public string PasswordHash { get; set; } = "";
    [FirestoreProperty("creadoEn")] public Timestamp CreatedAt { get; set; }
    [FirestoreProperty("google_uid")]
    public string GoogleUid { get; set; } = "";

}


