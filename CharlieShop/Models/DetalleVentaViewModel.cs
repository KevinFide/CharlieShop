
namespace CharlieShop.Models
{
    public class DetalleVentaViewModel
    {
        public int IdProducto { get; set; }

        public string Codigo { get; set; } = string.Empty;

        public string Nombre { get; set; } = string.Empty;

        public decimal Cantidad { get; set; }

        public decimal PrecioUnitario { get; set; }

        public decimal StockActual { get; set; }

        public decimal Subtotal =>
            Math.Round(Cantidad * PrecioUnitario, 2);
    }
}
