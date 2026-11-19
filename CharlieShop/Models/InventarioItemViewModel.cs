
using System;

namespace CharlieShop.Models
{
    public class InventarioItemViewModel
    {
        public int IdProducto { get; set; }
        public int? IdInventario { get; set; }

        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string? Categoria { get; set; }

        public decimal PrecioVenta { get; set; }
        public decimal StockActual { get; set; }
        public decimal StockMinimo { get; set; }
        public decimal? StockMaximo { get; set; }

        public string? Ubicacion { get; set; }
        public DateTime? FechaActualizacion { get; set; }

        public string? ImagenUrl { get; set; }
        public IFormFile? ImagenArchivo { get; set; }

        public bool StockBajo =>
            StockActual > 0 && StockActual <= StockMinimo;

        public bool SinExistencias =>
            StockActual == 0;
    }
}
