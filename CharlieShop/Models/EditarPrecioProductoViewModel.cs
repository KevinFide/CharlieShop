namespace CharlieShop.Models
{
    public class EditarPrecioProductoViewModel
    {
        public int IdProducto { get; set; }

        public string Nombre { get; set; } = string.Empty;

        public string Codigo { get; set; } = string.Empty;

        public decimal PrecioActual { get; set; }

        public decimal NuevoPrecio { get; set; }
    }
}