
using System.Collections.Generic;

namespace CharlieShop.Models
{
    public class InventarioIndexViewModel
    {
        public List<InventarioItemViewModel> Productos { get; set; }
            = new();

        public List<InventarioItemViewModel> ProductosStockBajo { get; set; }
            = new();

        public int TotalProductos { get; set; }
        public int TotalStockBajo { get; set; }
        public int TotalAgotados { get; set; }

        public string? Busqueda { get; set; }
        public string Estado { get; set; } = "TODOS";
    }
}
