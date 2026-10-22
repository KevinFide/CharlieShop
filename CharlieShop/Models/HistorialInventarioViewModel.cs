
using System;
using System.Collections.Generic;

namespace CharlieShop.Models
{
    public class HistorialInventarioViewModel
    {
        public List<MovimientoInventarioViewModel> Movimientos { get; set; }
            = new();

        public List<ProductoInventarioOpcionViewModel> Productos { get; set; }
            = new();

        public int? IdProducto { get; set; }
        public string TipoMovimiento { get; set; } = "TODOS";

        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }

        public int Pagina { get; set; } = 1;
        public int TamanoPagina { get; set; } = 10;
        public int TotalRegistros { get; set; }

        public int TotalPaginas =>
            TamanoPagina > 0
                ? (int)Math.Ceiling((double)TotalRegistros / TamanoPagina)
                : 0;

        public bool TienePaginaAnterior => Pagina > 1;
        public bool TienePaginaSiguiente => Pagina < TotalPaginas;
    }

    public class ProductoInventarioOpcionViewModel
    {
        public int IdProducto { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
    }
}
