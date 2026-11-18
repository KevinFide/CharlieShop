using System.ComponentModel.DataAnnotations;

namespace CharlieShop.Models
{
    public class CrearProductoViewModel
    {
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
        [Range(typeof(decimal), "0", "9999999999", ErrorMessage = "El precio debe ser mayor o igual a cero.")]
        public decimal PrecioVenta { get; set; }

        [Range(typeof(decimal), "0", "9999999999", ErrorMessage = "El costo debe ser mayor o igual a cero.")]
        public decimal? Costo { get; set; }

        [StringLength(20)]
        public string? UnidadMedida { get; set; }

        [Range(typeof(decimal), "0", "9999999999", ErrorMessage = "El stock inicial no puede ser negativo.")]
        public decimal StockInicial { get; set; } = 0;
    }
}
