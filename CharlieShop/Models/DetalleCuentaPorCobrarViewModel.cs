
using System;
using System.Collections.Generic;

namespace CharlieShop.Models
{
    public class DetalleCuentaPorCobrarViewModel
    {
        public int IdCxc { get; set; }
        public int IdCliente { get; set; }

        public string NombreCliente { get; set; } = string.Empty;
        public string? Identificacion { get; set; }
        public string? Telefono { get; set; }
        public string? Email { get; set; }

        public int IdVenta { get; set; }

        public DateTime Fecha { get; set; }
        public DateTime FechaVencimiento { get; set; }

        public decimal Total { get; set; }
        public decimal SaldoPendiente { get; set; }

        public string Estado { get; set; } = "PENDIENTE";
        public string FormaPago { get; set; } = "CREDITO";

        public List<HistorialPagoCuentaViewModel> Pagos { get; set; }
            = new();
    }
}
