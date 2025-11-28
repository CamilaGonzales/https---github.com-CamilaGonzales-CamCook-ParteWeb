using Google.Cloud.Firestore;

namespace CamCook.Models
{
    [FirestoreData]
    public class Recipe
    {
        [FirestoreDocumentId] public string Id { get; set; } = default!;

        [FirestoreProperty("titulo")]
        public string Title { get; set; } = "";

        [FirestoreProperty("calorias")]
        public int? Calories { get; set; }

        [FirestoreProperty("porciones")]
        public int? Servings { get; set; }

        [FirestoreProperty("tiempoPrep")]
        public string PrepTimeText { get; set; } = "";

        [FirestoreProperty("imagenUrl")]
        public string imagenUrl { get; set; }

        [FirestoreProperty("mainImageUrl")]
        public string mainImageUrl { get; set; }

        [FirestoreProperty("creadoEn")]
        public Timestamp CreatedAt { get; set; }

        // --- CAMPOS CORRECTOS ---
        [FirestoreProperty("likes")]
        public int? Likes { get; set; }

        [FirestoreProperty("views")]
        public int? Views { get; set; }

        // --- LISTAS ---
        [FirestoreProperty("ingredientes")]
        public List<Ingredient> Ingredients { get; set; } = new();

        [FirestoreProperty("pasos")]
        public List<CookStep> Steps { get; set; } = new();

        // --- AUTOR ---
        [FirestoreProperty("authorUid")]
        public string AuthorUid { get; set; } = "";

        [FirestoreProperty("authorEmail")]
        public string AuthorEmail { get; set; } = "";
    }

    [FirestoreData]
    public class Ingredient
    {
        [FirestoreProperty("nombre")] public string Name { get; set; } = "";
        [FirestoreProperty("cantidad")] public string Quantity { get; set; } = "";
        [FirestoreProperty("unidad")] public string? Unit { get; set; }
    }

    [FirestoreData]
    public class CookStep
    {
        [FirestoreProperty("descripcion")] public string Description { get; set; } = "";
        [FirestoreProperty("imagenUrl")] public string? ImageUrl { get; set; }
        [FirestoreProperty("orden")] public int Order { get; set; }
    }
}
