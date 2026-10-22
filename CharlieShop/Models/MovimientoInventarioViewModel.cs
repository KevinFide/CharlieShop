
using System;

namespace CharlieShop.Models
{
    public class MovimientoInventarioViewModel
    {
        public int IdMovimiento { get; set; }
        public int IdProducto { get; set; }
        public int IdInventario { get; set; }

        public string Codigo { get; set; } = string.Empty;
        public string NombreProducto { get; set; } = string.Empty;

        public string TipoMovimiento { get; set; } = string.Empty;
        public decimal Cantidad { get; set; }
        public DateTime Fecha { get; set; }

        public string? Referencia { get; set; }
        public string? Observaciones { get; set; }

        public int IdUsuario { get; set; }
        public string NombreUsuario { get; set; } = string.Empty;
    }
}
