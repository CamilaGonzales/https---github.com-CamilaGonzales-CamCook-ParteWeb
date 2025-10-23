using Google.Cloud.Firestore;

namespace CamCook.Models
{
    [FirestoreData]
    public class User
    {
        [FirestoreDocumentId] public string Id { get; set; } = "";
        [FirestoreProperty("nombre")] public string Username { get; set; } = "";
        [FirestoreProperty("correo")] public string Email { get; set; } = "";
        [FirestoreProperty("contraseña")] public string Password { get; set; } = "";
        [FirestoreProperty("rol")] public string Role { get; set; } = "usuario"; // usuario|chef|admin
        [FirestoreProperty("creadoEn")] public Timestamp CreatedAt { get; set; }
    }
}
