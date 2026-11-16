
using System.ComponentModel.DataAnnotations;

namespace CharlieShop.Models
{
    public class RegistrarPagoCuentaViewModel
    {
        public int IdCxc { get; set; }

        [Required(ErrorMessage = "El monto es obligatorio.")]
        [Range(
            0.01,
            9999999999,
            ErrorMessage = "El monto debe ser mayor que cero."
        )]
        public decimal Monto { get; set; }

        [Required(ErrorMessage = "Seleccione un método de pago.")]
        [StringLength(50)]
        public string MetodoPago { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Referencia { get; set; }

        public string? Observaciones { get; set; }

        // Información para mostrar en el formulario.
        public string NombreCliente { get; set; } = string.Empty;

        public decimal SaldoPendiente { get; set; }
    }
}
