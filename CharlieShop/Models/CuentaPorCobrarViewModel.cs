
using System;

namespace CharlieShop.Models
{
    public class CuentaPorCobrarViewModel
    {
        public int IdCxc { get; set; }
        public int IdCliente { get; set; }

        public string NombreCliente { get; set; } = string.Empty;
        public string? Identificacion { get; set; }

        public int IdVenta { get; set; }

        public DateTime Fecha { get; set; }
        public DateTime FechaVencimiento { get; set; }

        public decimal Total { get; set; }
        public decimal SaldoPendiente { get; set; }

        public string Estado { get; set; } = "PENDIENTE";

        public int DiasVencida { get; set; }

        // Propiedades para filtros de la pantalla principal.
        public string? Busqueda { get; set; }
        public string EstadoFiltro { get; set; } = "TODOS";
    }
}
