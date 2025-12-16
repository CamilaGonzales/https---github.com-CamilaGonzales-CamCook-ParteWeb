using System.ComponentModel.DataAnnotations;

namespace CamCook.Models
{
    public class LoginViewModel
    {
        [Required]
        public string Username { get; set; } = "";  // Nombre de usuario

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = "";  // Contraseña

        public string? ReturnUrl { get; set; }  // Opcional para redirigir a una página después de login
    }
}
