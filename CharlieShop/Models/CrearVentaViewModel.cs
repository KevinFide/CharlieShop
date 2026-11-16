
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CharlieShop.Models
{
    public class CrearVentaViewModel : IValidatableObject
    {
        public int? IdCliente { get; set; }

        [Required(ErrorMessage = "Seleccione una forma de pago.")]
        public string FormaPago { get; set; } = "CONTADO";

        [Range(
            0,
            100,
            ErrorMessage = "El descuento debe estar entre 0 y 100 %."
        )]
        public decimal DescuentoPorcentaje { get; set; } = 0;

        [Required(ErrorMessage = "Debe agregar productos a la venta.")]
        public string ProductosJson { get; set; } = "[]";

        public List<DetalleVentaViewModel> Productos { get; set; }
            = new();

        public DateTime? FechaVencimiento { get; set; }

        public decimal Subtotal =>
            Math.Round(Productos.Sum(p => p.Subtotal), 2);

        public decimal Descuento =>
            Math.Round(Subtotal * DescuentoPorcentaje / 100, 2);

        public decimal BaseImponible =>
            Math.Max(0, Subtotal - Descuento);

        public decimal Impuesto =>
            Math.Round(BaseImponible * 0.13m, 2);

        public decimal Total =>
            Math.Round(BaseImponible + Impuesto, 2);

        public int? IdVentaRegistrada { get; set; }

        public string? Mensaje { get; set; }

        public IEnumerable<ValidationResult> Validate(
            ValidationContext validationContext)
        {
            if (FormaPago == "CREDITO")
            {
                if (!FechaVencimiento.HasValue)
                {
                    yield return new ValidationResult(
                        "Debe establecer la fecha de vencimiento del crédito.",
                        new[] { nameof(FechaVencimiento) }
                    );
                }
                else if (FechaVencimiento.Value.Date < DateTime.Today)
                {
                    yield return new ValidationResult(
                        "La fecha de vencimiento no puede ser anterior a la fecha actual.",
                        new[] { nameof(FechaVencimiento) }
                    );
                }
            }
            else if (FormaPago == "CONTADO" &&
                     FechaVencimiento.HasValue)
            {
                yield return new ValidationResult(
                    "Las ventas de contado no deben tener fecha de vencimiento.",
                    new[] { nameof(FechaVencimiento) }
                );
            }
        }
    }
}
