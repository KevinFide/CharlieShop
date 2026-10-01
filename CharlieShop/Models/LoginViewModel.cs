using System.ComponentModel.DataAnnotations;

namespace CharlieShop.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Ingrese su nombre de usuario.")]
        [Display(Name = "Usuario")]
        public string Usuario { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ingrese su contraseña.")]
        [DataType(DataType.Password)]
        [Display(Name = "Contraseña")]
        public string Contrasena { get; set; } = string.Empty;

        public string? MensajeError { get; set; }
    }
}