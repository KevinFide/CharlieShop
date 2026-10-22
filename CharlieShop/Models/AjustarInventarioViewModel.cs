
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CharlieShop.Models
{
    public class AjustarInventarioViewModel
    {
        [Required(ErrorMessage = "Seleccione un producto.")]
        [Range(1, int.MaxValue, ErrorMessage = "Seleccione un producto válido.")]
        public int IdProducto { get; set; }

        [Required(ErrorMessage = "Seleccione la dirección del ajuste.")]
        [RegularExpression("^(ENTRADA|SALIDA)$",
            ErrorMessage = "La dirección debe ser ENTRADA o SALIDA.")]
        public string Direccion { get; set; } = "ENTRADA";

        [Required(ErrorMessage = "Ingrese la cantidad.")]
        public decimal Cantidad { get; set; }

        [StringLength(100, ErrorMessage = "La referencia admite hasta 100 caracteres.")]
        public string? Referencia { get; set; }

        public string? Observaciones { get; set; }

        public List<ProductoInventarioOpcionViewModel> Productos { get; set; }
            = new();
    }
}
