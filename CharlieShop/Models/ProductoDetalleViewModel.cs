using System.Collections.Generic;

namespace CharlieShop.Models
{
    public class ProductoDetalleViewModel
    {
        public Producto Producto { get; set; } = new Producto();

        public List<MovimientoInventarioViewModel> UltimosMovimientos { get; set; }
            = new List<MovimientoInventarioViewModel>();
    }
}
