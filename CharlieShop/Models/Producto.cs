namespace CharlieShop.Models
{
    public class Producto
    {
        public int IdProducto { get; set; }

        public string Codigo { get; set; } = string.Empty;

        public string Nombre { get; set; } = string.Empty;

        public string? Descripcion { get; set; }

        public string? Categoria { get; set; }

        public decimal PrecioVenta { get; set; }

        public decimal? Costo { get; set; }

        public string? UnidadMedida { get; set; }

        public bool Estado { get; set; }

        public DateTime FechaCreacion { get; set; }

        public decimal StockActual { get; set; }

        public decimal StockMinimo { get; set; }
    }
}