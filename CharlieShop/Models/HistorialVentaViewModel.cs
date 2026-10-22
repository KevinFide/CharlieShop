
using System;

namespace CharlieShop.Models
{
    public class HistorialVentaViewModel
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
    }
}
