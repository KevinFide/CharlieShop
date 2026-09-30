using System.ComponentModel.DataAnnotations;

namespace CharlieShop.Models
{
    public class EditarUsuarioViewModel
    {
        public int IdUsuario { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(
            100,
            ErrorMessage = "El nombre no puede superar los 100 caracteres.")]
        [Display(Name = "Nombre completo")]
        public string Nombre { get; set; } = string.Empty;


        [Required(ErrorMessage = "El nombre de usuario es obligatorio.")]
        [StringLength(
            50,
            ErrorMessage = "El nombre de usuario no puede superar los 50 caracteres.")]
        [Display(Name = "Nombre de usuario")]
        public string Usuario { get; set; } = string.Empty;


        [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
        [EmailAddress(ErrorMessage = "Ingrese un correo electrónico válido.")]
        [StringLength(
            100,
            ErrorMessage = "El correo electrónico no puede superar los 100 caracteres.")]
        [Display(Name = "Correo electrónico")]
        public string Email { get; set; } = string.Empty;


        [Required(ErrorMessage = "Debe seleccionar un rol.")]
        [Display(Name = "Rol")]
        public int RolId { get; set; }


        [Display(Name = "Estado")]
        public bool Estado { get; set; }


        public List<RolOpcionViewModel> Roles { get; set; }
            = new List<RolOpcionViewModel>();
    }


    public class RolOpcionViewModel
    {
        public int IdRol { get; set; }

        public string Nombre { get; set; } = string.Empty;
    }
}