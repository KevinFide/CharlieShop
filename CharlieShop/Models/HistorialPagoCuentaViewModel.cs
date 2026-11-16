
using System;

namespace CharlieShop.Models
{
    public class HistorialPagoCuentaViewModel
    {
        public int IdPago { get; set; }
        public int IdCxc { get; set; }
        public int IdCliente { get; set; }

        public string NombreCliente { get; set; } = string.Empty;
        public string? Identificacion { get; set; }

        public int IdVenta { get; set; }

        public DateTime FechaPago { get; set; }

        public decimal Monto { get; set; }

        public string MetodoPago { get; set; } = string.Empty;

        public string? Referencia { get; set; }
        public string? Observaciones { get; set; }

        public string Usuario { get; set; } = string.Empty;
    }
}
