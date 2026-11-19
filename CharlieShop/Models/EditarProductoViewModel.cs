using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace CharlieShop.Models
{
    public class EditarProductoViewModel
    {
        public int IdProducto { get; set; }

        [Display(Name = "Código")]
        [Required(ErrorMessage = "El código es obligatorio.")]
        [StringLength(50)]
        public string Codigo { get; set; } = string.Empty;

        [Display(Name = "Nombre del producto")]
        [Required(ErrorMessage = "El nombre del producto es obligatorio.")]
        [StringLength(150)]
        public string Nombre { get; set; } = string.Empty;

        [Display(Name = "Descripción")]
        public string? Descripcion { get; set; }

        [Display(Name = "Categoría")]
        [StringLength(100)]
        public string? Categoria { get; set; }

        [Display(Name = "Precio de Venta")]
        [Required(ErrorMessage = "El precio de venta es obligatorio.")]
        [Range(0, double.MaxValue, ErrorMessage = "El precio debe ser mayor o igual a cero.")]
        public decimal PrecioVenta { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "El costo debe ser mayor o igual a cero.")]
        public decimal? Costo { get; set; }

        [Display(Name = "Unidad de medida")]
        [StringLength(20)]
        public string? UnidadMedida { get; set; }
        public string? ImagenUrl { get; set; }
        public IFormFile? ImagenArchivo { get; set; }
    }
}