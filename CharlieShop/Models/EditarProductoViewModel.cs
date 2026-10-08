using System.ComponentModel.DataAnnotations;

namespace CharlieShop.Models
{
    public class EditarProductoViewModel
    {
        public int IdProducto { get; set; }

        [Required(ErrorMessage = "El código es obligatorio.")]
        [StringLength(50)]
        public string Codigo { get; set; } = string.Empty;

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(150)]
        public string Nombre { get; set; } = string.Empty;

        public string? Descripcion { get; set; }

        [StringLength(100)]
        public string? Categoria { get; set; }

        [Required(ErrorMessage = "El precio de venta es obligatorio.")]
        [Range(0, double.MaxValue, ErrorMessage = "El precio debe ser mayor o igual a cero.")]
        public decimal PrecioVenta { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "El costo debe ser mayor o igual a cero.")]
        public decimal? Costo { get; set; }

        [StringLength(20)]
        public string? UnidadMedida { get; set; }
    }
}