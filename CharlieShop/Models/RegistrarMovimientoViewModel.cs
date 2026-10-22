
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CharlieShop.Models
{
    public class RegistrarMovimientoViewModel
    {
        [Required(ErrorMessage = "Debés seleccionar un producto.")]
        [Range(1, int.MaxValue, ErrorMessage = "Seleccioná un producto válido.")]
        public int IdProducto { get; set; }

        [Required(ErrorMessage = "Debés seleccionar el tipo de movimiento.")]
        public string TipoMovimiento { get; set; } = "ENTRADA";

        [Required(ErrorMessage = "La cantidad es obligatoria.")]
        public decimal Cantidad { get; set; }

        [StringLength(100, ErrorMessage = "La referencia no puede superar los 100 caracteres.")]
        public string? Referencia { get; set; }

        public string? Observaciones { get; set; }

        public List<ProductoInventarioOpcionViewModel> Productos { get; set; }
            = new List<ProductoInventarioOpcionViewModel>();
    }
}
