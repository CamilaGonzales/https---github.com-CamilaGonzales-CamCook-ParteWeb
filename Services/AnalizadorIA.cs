using System.Text;
using System.Text.RegularExpressions;

namespace CamCook.Services;

public enum AiVerdict { Ok, Review, Reject }
public record AiResult(double Score, List<string> Flags, AiVerdict Verdict);

public static class AnalizadorIA
{
    static readonly string[] Vulgaridades = { "puta", "puto", "mierda", "imbecil", "idiota", "estupido", "hdp" };
    static readonly string[] Ilegales = {
        "cocaína","metanfetamina","lsd","xtc","éxtasis","marihuana prensada",
        "pistola","revólver","rifle","fusil","munición","silenciador","granada"
    };
    static readonly Regex RxUrl = new(@"https?://|www\.", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex RxMail = new(@"[a-z0-9._%+-]+@[a-z0-9.-]+\.[a-z]{2,}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex RxPhone = new(@"\+?\d[\d\s\-().]{6,}", RegexOptions.Compiled);
    static readonly Regex RxAt = new(@"(^|\s)@[a-z0-9_.]{3,}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    static string NormalizeLite(string s)
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

    /// <summary>
    /// Analiza título + “descripcionCompuesta” (puedes pasar Steps/Notas) + flags varios.
    /// </summary>
    public static AiResult Analizar(
        string titulo,
        string descripcionCompuesta,
        int ingredientesCount,
        bool tieneImagen)
    {
        double score = 1.0;
        var flags = new List<string>();

        titulo = NormalizeLite(titulo);
        var desc = NormalizeLite(descripcionCompuesta);
        var texto = $"{titulo} {desc}";

        // Vacíos / muy corto
        if (string.IsNullOrWhiteSpace(titulo) || string.IsNullOrWhiteSpace(desc))
        { score -= 0.50; flags.Add("Campos incompletos"); }
        var palabras = desc.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (palabras.Length < 30) { score -= 0.20; flags.Add("Descripción demasiado corta"); }

        // Ingredientes
        if (ingredientesCount < 2) { score -= 0.10; flags.Add("Pocos ingredientes"); }

        // Contacto externo
        if (RxUrl.IsMatch(texto)) { score -= 0.25; flags.Add("Contiene URL"); }
        if (RxMail.IsMatch(texto)) { score -= 0.20; flags.Add("Contiene email"); }
        if (RxPhone.IsMatch(texto)) { score -= 0.20; flags.Add("Contiene teléfono"); }
        if (RxAt.IsMatch(texto)) { score -= 0.10; flags.Add("Mención @usuario"); }

        // Mayúsculas excesivas
        int mayus = desc.Count(char.IsUpper);
        int letras = desc.Count(char.IsLetter);
        if (letras > 0 && (double)mayus / letras > 0.35)
        { score -= 0.10; flags.Add("MAYÚSCULAS excesivas"); }

        // Vulgaridades + ilegales
        // Mejora: normalizamos a minúsculas y tokenizamos solo letras/dígitos para detectar variantes
        var simple = Regex.Replace(texto.ToLowerInvariant(), "[^a-z0-9s]", " ");
        var tokens = simple.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

        var foundVulgar = new List<string>();
        var foundIlegal = new List<string>();

        // Buscar coincidencias directas en tokens
        foreach (var w in Vulgaridades)
        {
            if (tokens.Contains(w.ToLowerInvariant())) foundVulgar.Add(w);
        }

        foreach (var w in Ilegales)
        {
            if (tokens.Contains(w.ToLowerInvariant())) foundIlegal.Add(w);
        }

        // Buscar pares ofensivos adyacentes (ej. "puto idiota") o si aparecen como dos tokens cercanos
        if (foundVulgar.Count == 0)
        {
            for (int i = 0; i < tokens.Count - 1; i++)
            {
                var a = tokens[i];
                var b = tokens[i + 1];
                if (Vulgaridades.Contains(a) && Vulgaridades.Contains(b))
                {
                    if (!foundVulgar.Contains(a)) foundVulgar.Add(a);
                    if (!foundVulgar.Contains(b)) foundVulgar.Add(b);
                }
            }
        }

        // Si encontramos algo prohibido o vulgar, agregamos flags y forzamos rechazo
        if (foundIlegal.Count > 0)
        {
            foreach (var it in foundIlegal) flags.Add($"Contenido prohibido: {it}");
            // puntaje mínimo
            score = 0;
            return new AiResult(score, flags, AiVerdict.Reject);
        }

        if (foundVulgar.Count > 0)
        {
            foreach (var it in foundVulgar) flags.Add($"Lenguaje vulgar: {it}");
            score = 0;
            return new AiResult(score, flags, AiVerdict.Reject);
        }

        // Si no encontramos coincidencias críticas, mantenemos heurísticas previas
        score = Math.Clamp(score, 0, 1);
        var verdict = score >= 0.80 ? AiVerdict.Ok
                    : score >= 0.50 ? AiVerdict.Review
                    : AiVerdict.Reject;

        return new AiResult(score, flags, verdict);
    }
}
