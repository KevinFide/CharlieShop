namespace CharlieShop.Models
{
    public class DashboardViewModel
    {
        public decimal VentasMes { get; set; }
        public int TotalProductos { get; set; }
        public int TotalClientes { get; set; }
        public decimal CuentasPorCobrar { get; set; }
        public decimal VentasHoy { get; set; }
        public int TransaccionesHoy { get; set; }
        public int ProductosStockBajo { get; set; }
        public int CuentasPendientes { get; set; }

        public List<VentaRecienteViewModel> VentasRecientes { get; set; } = new();
        public List<VentaMensualViewModel> VentasUltimosMeses { get; set; } = new();
        public List<ProductoStockBajoViewModel> ProductosBajoStock { get; set; } = new();
    }

    public class VentaRecienteViewModel
    {
        public int IdVenta { get; set; }
        public string Cliente { get; set; } = "Cliente general";
        public DateTime Fecha { get; set; }
        public decimal TotalNeto { get; set; }
        public string Estado { get; set; } = "";
    }

    public class VentaMensualViewModel
    {
        public DateTime Fecha { get; set; }
        public decimal Total { get; set; }
    }

    public class ProductoStockBajoViewModel
    {
        public string Nombre { get; set; } = "";
        public decimal StockActual { get; set; }
        public decimal StockMinimo { get; set; }
    }
}
