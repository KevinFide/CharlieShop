using System.ComponentModel.DataAnnotations;

namespace CharlieShop.Models
{
    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
        [EmailAddress(ErrorMessage = "Ingrese un correo electrónico válido.")]
        [StringLength(
            100,
            ErrorMessage = "El correo electrónico no puede superar los 100 caracteres."
        )]
        [Display(Name = "Correo electrónico")]
        public string Email { get; set; } = string.Empty;
    }
}