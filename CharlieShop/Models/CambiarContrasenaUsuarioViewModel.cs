using System.ComponentModel.DataAnnotations;

namespace CharlieShop.Models
{
    public class CambiarContrasenaUsuarioViewModel
    {
        public int IdUsuario { get; set; }

        [Required(ErrorMessage = "La nueva contraseña es obligatoria.")]
        [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
        [Display(Name = "Nueva contraseña")]
        public string NuevaContrasena { get; set; } = string.Empty;

        [Required(ErrorMessage = "Debe confirmar la nueva contraseña.")]
        [Compare(
            "NuevaContrasena",
            ErrorMessage = "Las contraseñas no coinciden."
        )]
        [Display(Name = "Confirmar contraseña")]
        public string ConfirmarContrasena { get; set; } = string.Empty;
    }
}