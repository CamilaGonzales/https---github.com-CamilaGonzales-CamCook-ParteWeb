using System.Text.RegularExpressions;

namespace CamCook.Services.Security
{
    public static class PasswordValidator
    {
        // 8–64, al menos 1 minúscula, 1 mayúscula, 1 dígito, 1 símbolo, sin espacios
        private static readonly Regex BaseRegex =
            new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9])\S{8,64}$",
                      RegexOptions.Compiled);

        private static readonly Regex RepeatsRegex =
            new Regex(@"(.)\1\1", RegexOptions.Compiled); // 3+ repeticiones

        private static readonly HashSet<string> Blacklist = new(StringComparer.OrdinalIgnoreCase)
        {
            "123456","12345678","123456789","qwerty","password","111111","123123",
            "abc123","admin","letmein","iloveyou","000000","qwertyuiop","passw0rd"
        };

        public static bool Validate(string password, string? email, string? nombre, out List<string> errors)
        {
            errors = new List<string>();

            if (string.IsNullOrWhiteSpace(password))
            {
                errors.Add("La contraseña es obligatoria.");
                return false;
            }

            if (!BaseRegex.IsMatch(password))
                errors.Add("Debe tener 8–64 caracteres, incluir mayúscula, minúscula, número y símbolo, y no contener espacios.");

            if (RepeatsRegex.IsMatch(password))
                errors.Add("Evita repeticiones de 3+ caracteres iguales seguidos (p. ej., 'aaa', '111').");

            if (Blacklist.Contains(password))
                errors.Add("La contraseña es demasiado común. Elige otra.");

            // Evitar que contenga nombre/usuario/email (parte local)
            var tokens = new List<string>();
            if (!string.IsNullOrWhiteSpace(email))
            {
                var at = email.IndexOf('@');
                if (at > 0) tokens.Add(email[..at]);
            }
            if (!string.IsNullOrWhiteSpace(nombre))
            {
                foreach (var part in nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    tokens.Add(part);
            }

            foreach (var token in tokens.Where(t => t.Length >= 3))
            {
                if (password.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("No incluyas tu nombre o usuario/email en la contraseña.");
                    break;
                }
            }

            if (password.Length < 10)
                errors.Add("Recomendado: usa 10 caracteres o más para mayor seguridad.");

            return errors.Count == 0;
        }
    }
}
