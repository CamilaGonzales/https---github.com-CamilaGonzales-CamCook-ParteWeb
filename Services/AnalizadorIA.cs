using System.Text;
using System.Text.RegularExpressions;

public enum AiVerdict { Ok, Review, Reject }

public record AiResult(double Score, List<string> Flags, AiVerdict Verdict);

public static class AnalizadorIA
{
    // Listas base (puedes cargar desde JSON más adelante)
    static readonly string[] Vulgaridades = new[]
    {
        "puta","puto","mierda","imbecil","idiota","estupido","cállate","hdp"
    };

    static readonly string[] Odio = new[]
    {
        "maldito [a-záéíóúñ]+","muerte a","exterminar","basura humana"
    };

    static readonly string[] Ilegales = new[]
    {
        "cocaína","metanfetamina","lsd","xtc","éxtasis","marihuana prensada",
        "pistola","revólver","rifle","fusil","munición","silenciador","granada",
        "anabólicos ilegales","dólares falsos","documentos falsos"
    };

    // Detectores de contacto/venta externa
    static readonly Regex RxUrl = new(@"https?://|www\.", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex RxMail = new(@"[a-z0-9._%+-]+@[a-z0-9.-]+\.[a-z]{2,}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex RxPhone = new(@"\+?\d[\d\s\-().]{6,}", RegexOptions.Compiled);
    static readonly Regex RxAt = new(@"(^|\s)@[a-z0-9_.]{3,}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    static string Normalizar(string s)
    {
        s ??= string.Empty;
        s = s.Trim();
        var nfkd = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in nfkd)
        {
            var uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
            if (uc != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(ch);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    public static AiResult Analizar(
        string titulo,
        string descripcion,
        IReadOnlyList<string> ingredientes,
        bool tieneImagen)
    {
        double score = 1.0;
        var flags = new List<string>();

        titulo = Normalizar(titulo);
        descripcion = Normalizar(descripcion);
        var texto = $"{titulo} {descripcion}";

        // 1) Vacíos / muy cortos
        if (string.IsNullOrWhiteSpace(titulo) || string.IsNullOrWhiteSpace(descripcion))
        {
            score -= 0.50; flags.Add("Campos incompletos (título/descr.)");
        }
        var palabras = descripcion.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (palabras.Length < 30) { score -= 0.20; flags.Add("Descripción demasiado corta (<30 palabras)"); }

        // 2) Ingredientes
        if (ingredientes == null || ingredientes.Count < 2)
        { score -= 0.10; flags.Add("Pocos ingredientes"); }

        // 3) Imagen
        if (!tieneImagen) { score -= 0.10; flags.Add("Sin imagen"); }

        // 4) Contacto externo / venta fuera de la app
        if (RxUrl.IsMatch(texto)) { score -= 0.25; flags.Add("Contiene URL"); }
        if (RxMail.IsMatch(texto)) { score -= 0.20; flags.Add("Contiene email"); }
        if (RxPhone.IsMatch(texto)) { score -= 0.20; flags.Add("Contiene teléfono"); }
        if (RxAt.IsMatch(texto)) { score -= 0.10; flags.Add("Mención de usuario @..."); }

        // 5) Mayúsculas excesivas (gritos)
        int mayus = descripcion.Count(char.IsUpper);
        int letras = descripcion.Count(char.IsLetter);
        if (letras > 0 && (double)mayus / letras > 0.35)
        { score -= 0.10; flags.Add("Uso excesivo de MAYÚSCULAS"); }

        // 6) Vulgaridades / odio
        foreach (var w in Vulgaridades)
        {
            if (Regex.IsMatch(texto, $@"\b{Regex.Escape(w)}\b", RegexOptions.IgnoreCase))
            { score -= 0.50; flags.Add($"Lenguaje vulgar: {w}"); }
        }
        foreach (var p in Odio)
        {
            if (Regex.IsMatch(texto, p, RegexOptions.IgnoreCase))
            { score -= 0.60; flags.Add("Lenguaje de odio/violento"); }
        }

        // 7) Productos ilegales / armas / drogas
        foreach (var w in Ilegales)
        {
            if (Regex.IsMatch(texto, $@"\b{Regex.Escape(w)}\b", RegexOptions.IgnoreCase))
            { score -= 0.80; flags.Add($"Contenido prohibido: {w}"); }
        }

        // Normaliza 0..1
        score = Math.Clamp(score, 0, 1);

        var verdict = score >= 0.80 ? AiVerdict.Ok
                    : score >= 0.50 ? AiVerdict.Review
                    : AiVerdict.Reject;

        return new AiResult(score, flags, verdict);
    }
}
