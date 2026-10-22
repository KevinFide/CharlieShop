
using System;
using System.Collections.Generic;

namespace CharlieShop.Models
{
    public class ConsultaDetalleVentaViewModel
    {
        public int IdVenta { get; set; }

        public DateTime Fecha { get; set; }

        public string? NombreCliente { get; set; }

        public string? NombreVendedor { get; set; }

        public decimal Total { get; set; }

        public decimal Descuento { get; set; }

        public decimal Impuesto { get; set; }

        public decimal TotalNeto { get; set; }

        public string? FormaPago { get; set; }

        public string? Estado { get; set; }

        public List<ProductoConsultaDetalleVentaViewModel> Productos { get; set; }
            = new List<ProductoConsultaDetalleVentaViewModel>();
    }

    public class ProductoConsultaDetalleVentaViewModel
    {
        public int IdDetalleVenta { get; set; }

        public int IdProducto { get; set; }

        public string? CodigoProducto { get; set; }

        public string? NombreProducto { get; set; }

        public decimal Cantidad { get; set; }

        public decimal PrecioUnitario { get; set; }

        public decimal Descuento { get; set; }

        public decimal Impuesto { get; set; }

        public decimal Subtotal { get; set; }
    }
}
