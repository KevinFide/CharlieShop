using System.ComponentModel.DataAnnotations;

namespace CharlieShop.Models
{
    public class CambiarContrasenaViewModel
    {
        [Required(ErrorMessage = "La contraseña actual es obligatoria.")]
        [DataType(DataType.Password)]
        [Display(Name = "Contraseña actual")]
        public string ContrasenaActual { get; set; } = string.Empty;


        [Required(ErrorMessage = "La nueva contraseña es obligatoria.")]
        [MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
        [DataType(DataType.Password)]
        [Display(Name = "Nueva contraseña")]
        public string NuevaContrasena { get; set; } = string.Empty;


        [Required(ErrorMessage = "Debe confirmar la nueva contraseña.")]
        [Compare(
            "NuevaContrasena",
            ErrorMessage = "Las contraseñas no coinciden."
        )]
        [DataType(DataType.Password)]
        [Display(Name = "Confirmar nueva contraseña")]
        public string ConfirmarContrasena { get; set; } = string.Empty;
    }
}