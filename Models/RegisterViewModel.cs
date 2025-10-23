using System.ComponentModel.DataAnnotations;

namespace CamCook.Models
{
    public class RegisterViewModel
    {
        [Required]
        public string Username { get; set; } = "";  // Nombre de usuario

        [Required, EmailAddress]
        public string Email { get; set; } = "";  // Correo electrónico

        [Required, DataType(DataType.Password), MinLength(6)]
        public string Password { get; set; } = "";  // Contraseña

        [Required, DataType(DataType.Password), Compare(nameof(Password))]
        public string ConfirmPassword { get; set; } = "";  // Confirmar contraseña
    }
}
