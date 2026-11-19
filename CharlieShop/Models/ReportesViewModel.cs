namespace CharlieShop.Models
{
    public class ReportesViewModel
    {
        public string ReporteActual { get; set; } = "analisis";
        public DateTime FechaInicio { get; set; } = DateTime.Today.AddDays(-29);
        public DateTime FechaFin { get; set; } = DateTime.Today;
        public string? Mensaje { get; set; }
        public string? Error { get; set; }

        public List<ReporteVentaItem> Ventas { get; set; } = new();
        public List<ReporteProductoVendidoItem> ProductosMasVendidos { get; set; } = new();
        public List<ReporteInventarioItem> Inventario { get; set; } = new();
        public List<ReporteCuentaPorCobrarItem> CuentasPorCobrar { get; set; } = new();
        public List<ReporteClienteItem> Clientes { get; set; } = new();

        // Datos del panel Análisis general: permanecen dentro del ViewModel principal.
        public ReporteAnalisisGeneral AnalisisGeneral { get; set; } = new();

        public decimal TotalVentas => Ventas.Sum(x => x.TotalNeto);
        public int CantidadVentas => Ventas.Count;
        public decimal TotalUnidadesVendidas => ProductosMasVendidos.Sum(x => x.CantidadVendida);
        public decimal TotalPorCobrar => CuentasPorCobrar.Sum(x => x.SaldoPendiente);
        public int ProductosCriticos => Inventario.Count(x => x.StockActual <= x.StockMinimo);
    }

    public class ReporteAnalisisGeneral
    {
        public decimal TicketPromedio { get; set; }
        public decimal UnidadesVendidas { get; set; }
        public int ClientesCompradores { get; set; }
        public decimal VentasCredito { get; set; }
        public decimal VentasContado { get; set; }
        public int CantidadVentas { get; set; }
        public List<ReporteAnalisisVentaDia> VentasPorDia { get; set; } = new();
        public List<ReporteAnalisisFormaPago> FormasPago { get; set; } = new();
        public List<ReporteAnalisisProducto> ProductosTop { get; set; } = new();
        public List<ReporteAnalisisMovimientoTipo> MovimientosPorTipo { get; set; } = new();
        public List<ReporteAnalisisCliente> ClientesTop { get; set; } = new();
        public List<ReporteAnalisisMovimiento> UltimosMovimientos { get; set; } = new();

        public decimal TotalFormaPago => FormasPago.Sum(x => x.Total);
        public decimal TotalMovimientos => MovimientosPorTipo.Sum(x => x.Unidades);
        public decimal MaxVentaDia => VentasPorDia.Count == 0 ? 0 : VentasPorDia.Max(x => x.Total);
        public decimal MaxProducto => ProductosTop.Count == 0 ? 0 : ProductosTop.Max(x => x.TotalVendido);
    }

    public class ReporteAnalisisVentaDia
    {
        public DateTime Fecha { get; set; }
        public decimal Total { get; set; }
    }

    public class ReporteAnalisisFormaPago
    {
        public string FormaPago { get; set; } = "";
        public int Cantidad { get; set; }
        public decimal Total { get; set; }
    }

    public class ReporteAnalisisProducto
    {
        public string Producto { get; set; } = "";
        public string Categoria { get; set; } = "";
        public decimal Unidades { get; set; }
        public decimal TotalVendido { get; set; }
    }

    public class ReporteAnalisisMovimientoTipo
    {
        public string TipoMovimiento { get; set; } = "";
        public int CantidadMovimientos { get; set; }
        public decimal Unidades { get; set; }
    }

    public class ReporteAnalisisCliente
    {
        public string Cliente { get; set; } = "";
        public int CantidadCompras { get; set; }
        public decimal TotalCompras { get; set; }
    }

    public class ReporteAnalisisMovimiento
    {
        public DateTime Fecha { get; set; }
        public string Producto { get; set; } = "";
        public string TipoMovimiento { get; set; } = "";
        public decimal Cantidad { get; set; }
        public string? Referencia { get; set; }
    }

    public class ReporteVentaItem
    {
        public int IdVenta { get; set; }
        public DateTime Fecha { get; set; }
        public string Cliente { get; set; } = "Cliente general";
        public decimal TotalNeto { get; set; }
        public string FormaPago { get; set; } = "";
        public string Estado { get; set; } = "";
    }

    public class ReporteProductoVendidoItem
    {
        public string Producto { get; set; } = "";
        public string Categoria { get; set; } = "";
        public decimal CantidadVendida { get; set; }
        public decimal TotalVendido { get; set; }
    }

    public class ReporteInventarioItem
    {
        public string Codigo { get; set; } = "";
        public string Producto { get; set; } = "";
        public string Categoria { get; set; } = "";
        public decimal StockActual { get; set; }
        public decimal StockMinimo { get; set; }
        public decimal? StockMaximo { get; set; }
        public string EstadoStock { get; set; } = "";
    }

    public class ReporteCuentaPorCobrarItem
    {
        public int IdCuenta { get; set; }
        public int IdVenta { get; set; }
        public string Cliente { get; set; } = "";
        public DateTime Fecha { get; set; }
        public DateTime FechaVencimiento { get; set; }
        public decimal Total { get; set; }
        public decimal SaldoPendiente { get; set; }
        public string Estado { get; set; } = "";
        public int DiasVencida { get; set; }
    }

    public class ReporteClienteItem
    {
        public int IdCliente { get; set; }
        public string NombreCompleto { get; set; } = "";
        public string? Identificacion { get; set; }
        public string? Telefono { get; set; }
        public string? Email { get; set; }
        public DateTime FechaRegistro { get; set; }
        public int CantidadCompras { get; set; }
        public decimal TotalCompras { get; set; }
        public decimal SaldoPendiente { get; set; }
    }
}
