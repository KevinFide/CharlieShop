using System.Data;
using System.Globalization;
using System.Net;
using System.Text;
using System.Security.Claims;
using System.Text.Json;
using CharlieShop.Data;
using CharlieShop.Models;
using CharlieShop.Services;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace CharlieShop.Controllers
{
    [Authorize]
    public class VentasController : Controller
    {
        private readonly DbConnection _dbConnection;
        private readonly EmailService _emailService;

        private readonly JsonSerializerOptions _jsonOptions =
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

        public VentasController(
            DbConnection dbConnection,
            EmailService emailService)
        {
            _dbConnection = dbConnection;
            _emailService = emailService;
        }

        // GET: /Ventas
        [HttpGet]
        public IActionResult Index()
        {
            try
            {
                using var connection = _dbConnection.CreateConnection();

                var fila = connection.QuerySingle(
                    @"CALL sp_consultar_historial_ventas(NULL::JSON)",
                    commandType: CommandType.Text
                );

                string? json = Convert.ToString(fila.o_ventas);

                var ventas = string.IsNullOrWhiteSpace(json)
                    ? new List<HistorialVentaViewModel>()
                    : JsonSerializer.Deserialize<List<HistorialVentaViewModel>>(
                        json,
                        _jsonOptions
                    ) ?? new List<HistorialVentaViewModel>();

                return View(ventas);
            }
            catch (PostgresException)
            {
                ViewBag.ErrorHistorial =
                    "No fue posible consultar el historial de ventas. " +
                    "Verifique el procedimiento almacenado y la conexión " +
                    "con la base de datos.";

                return View(new List<HistorialVentaViewModel>());
            }
            catch (Exception)
            {
                ViewBag.ErrorHistorial =
                    "Ocurrió un error al cargar el historial de ventas.";

                return View(new List<HistorialVentaViewModel>());
            }
        }

        // GET: /Ventas/Detalle/1
        [HttpGet]
        public IActionResult Detalle(int id)
        {
            if (id <= 0)
            {
                return BadRequest(
                    "El identificador de la venta no es válido."
                );
            }

            try
            {
                using var connection = _dbConnection.CreateConnection();

                var venta = connection.QuerySingleOrDefault<
                    ConsultaDetalleVentaViewModel>(
                    @"
                    SELECT
                        v.id_venta AS IdVenta,
                        v.fecha AS Fecha,
                        NULLIF(
                            BTRIM(
                                CONCAT_WS(
                                    ' ',
                                    c.nombre,
                                    c.apellido
                                )
                            ),
                            ''
                        ) AS NombreCliente,
                        u.nombre AS NombreVendedor,
                        v.total AS Total,
                        v.descuento AS Descuento,
                        v.impuesto AS Impuesto,
                        v.total_neto AS TotalNeto,
                        v.forma_pago AS FormaPago,
                        v.estado AS Estado
                    FROM venta v
                    LEFT JOIN cliente c
                        ON c.id_cliente = v.cliente_id
                    LEFT JOIN usuario u
                        ON u.id_usuario = v.usuario_id
                    WHERE v.id_venta = @IdVenta;
                    ",
                    new { IdVenta = id }
                );

                if (venta == null)
                {
                    return NotFound(
                        $"La venta #{id} no fue encontrada."
                    );
                }

                var productos = connection.Query<
                    ProductoConsultaDetalleVentaViewModel>(
                    @"
                    SELECT
                        dv.id_detalle_venta AS IdDetalleVenta,
                        dv.producto_id AS IdProducto,
                        p.codigo AS CodigoProducto,
                        p.nombre AS NombreProducto,
                        dv.cantidad AS Cantidad,
                        dv.precio_unitario AS PrecioUnitario,
                        dv.descuento AS Descuento,
                        dv.impuesto AS Impuesto,
                        dv.subtotal AS Subtotal
                    FROM detalle_venta dv
                    LEFT JOIN producto p
                        ON p.id_producto = dv.producto_id
                    WHERE dv.venta_id = @IdVenta
                    ORDER BY dv.id_detalle_venta;
                    ",
                    new { IdVenta = id }
                ).ToList();

                venta.Productos = productos;

                return View(venta);
            }
            catch (PostgresException)
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "Ocurrió un error al consultar el detalle de la venta."
                );
            }
            catch (Exception)
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "No fue posible cargar el detalle de la venta."
                );
            }
        }

        // GET: /Ventas/Crear
        [HttpGet]
        public IActionResult Crear()
        {
            var model = new CrearVentaViewModel();

            if (TempData["VentaExitosa"] != null)
            {
                model.Mensaje = TempData["VentaExitosa"]?.ToString();
            }

            if (TempData["IdVenta"] != null)
            {
                model.IdVentaRegistrada =
                    Convert.ToInt32(TempData["IdVenta"]);
            }

            if (TempData["ErrorVenta"] != null)
            {
                model.Mensaje = TempData["ErrorVenta"]?.ToString();
            }

            return View(model);
        }

        // GET: /Ventas/BuscarClientes?busqueda=...
        [HttpGet]
        public IActionResult BuscarClientes(string? busqueda)
        {
            try
            {
                using var connection = _dbConnection.CreateConnection();

                const string sql = @"
                    SELECT
                        id_cliente AS ""IdCliente"",
                        nombre AS ""Nombre"",
                        apellido AS ""Apellido"",
                        identificacion AS ""Identificacion""
                    FROM cliente
                    WHERE estado = TRUE
                      AND (
                          NULLIF(BTRIM(@Busqueda), '') IS NULL
                          OR nombre ILIKE '%' || BTRIM(@Busqueda) || '%'
                          OR apellido ILIKE '%' || BTRIM(@Busqueda) || '%'
                          OR identificacion ILIKE '%' || BTRIM(@Busqueda) || '%'
                      )
                    ORDER BY nombre, apellido
                    LIMIT 25;";

                var clientes = connection.Query(
                    sql,
                    new { Busqueda = busqueda?.Trim() ?? string.Empty }
                )
                .Select(c => new
                {
                    idCliente = (int)c.IdCliente,
                    nombre = (string)c.Nombre,
                    apellido = (string)c.Apellido,
                    identificacion = (string?)c.Identificacion
                })
                .ToList();

                return Json(clientes);
            }
            catch (Exception)
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new { mensaje = "No fue posible consultar los clientes." }
                );
            }
        }

        // GET: /Ventas/BuscarProductos?busqueda=...
        [HttpGet]
        public IActionResult BuscarProductos(string? busqueda)
        {
            try
            {
                using var connection = _dbConnection.CreateConnection();

                var fila = connection.QuerySingle(
                    @"CALL sp_buscar_productos_venta(
                        CAST(@p_busqueda AS VARCHAR),
                        NULL::JSON
                    )",
                    new
                    {
                        p_busqueda = busqueda?.Trim() ?? string.Empty
                    },
                    commandType: CommandType.Text
                );

                string? json = Convert.ToString(fila.o_productos);

                if (string.IsNullOrWhiteSpace(json))
                {
                    return Json(new List<ProductoVentaViewModel>());
                }

                var productos =
                    JsonSerializer.Deserialize<List<ProductoVentaViewModel>>(
                        json,
                        _jsonOptions
                    ) ?? new List<ProductoVentaViewModel>();

                return Json(productos);
            }
            catch (PostgresException ex)
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        mensaje =
                            $"Error PostgreSQL al buscar productos: {ex.MessageText}"
                    }
                );
            }
            catch (Exception ex)
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        mensaje = $"Error al buscar productos: {ex.Message}"
                    }
                );
            }
        }

        // GET: /Ventas/ObtenerProducto?idProducto=1
        [HttpGet]
        public IActionResult ObtenerProducto(int idProducto)
        {
            if (idProducto <= 0)
            {
                return BadRequest(new
                {
                    mensaje = "El identificador del producto no es válido."
                });
            }

            try
            {
                using var connection = _dbConnection.CreateConnection();

                var fila = connection.QuerySingle(
                    @"CALL sp_obtener_producto_venta(
                        @p_id_producto,
                        NULL::JSON
                    )",
                    new
                    {
                        p_id_producto = idProducto
                    },
                    commandType: CommandType.Text
                );

                string? json = Convert.ToString(fila.o_producto);

                if (string.IsNullOrWhiteSpace(json) || json == "null")
                {
                    return NotFound(new
                    {
                        mensaje = "El producto no existe o está inactivo."
                    });
                }

                var producto =
                    JsonSerializer.Deserialize<ProductoVentaViewModel>(
                        json,
                        _jsonOptions
                    );

                if (producto == null)
                {
                    return NotFound(new
                    {
                        mensaje = "No se encontró el producto."
                    });
                }

                return Json(producto);
            }
            catch (PostgresException ex)
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        mensaje =
                            $"Error PostgreSQL al consultar el producto: {ex.MessageText}"
                    }
                );
            }
            catch (Exception ex)
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        mensaje = $"Error al consultar el producto: {ex.Message}"
                    }
                );
            }
        }

        // POST: /Ventas/Finalizar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Finalizar(CrearVentaViewModel model)
        {
            List<DetalleVentaViewModel> productos;

            try
            {
                productos =
                    JsonSerializer.Deserialize<List<DetalleVentaViewModel>>(
                        model.ProductosJson ?? "[]",
                        _jsonOptions
                    ) ?? new List<DetalleVentaViewModel>();
            }
            catch (JsonException)
            {
                TempData["ErrorVenta"] =
                    "Los productos enviados no tienen un formato válido.";

                return RedirectToAction(nameof(Crear));
            }

            if (productos.Count == 0)
            {
                TempData["ErrorVenta"] =
                    "Debe agregar al menos un producto a la venta.";

                return RedirectToAction(nameof(Crear));
            }

            if (productos.Any(p =>
                p.IdProducto <= 0 ||
                p.Cantidad <= 0 ||
                p.Cantidad != decimal.Round(p.Cantidad, 2)))
            {
                TempData["ErrorVenta"] =
                    "Verifique que los productos tengan una cantidad válida mayor que cero.";

                return RedirectToAction(nameof(Crear));
            }

            if (productos.GroupBy(p => p.IdProducto)
                         .Any(g => g.Count() > 1))
            {
                TempData["ErrorVenta"] =
                    "Hay productos repetidos en el carrito.";

                return RedirectToAction(nameof(Crear));
            }

            if (model.FormaPago != "CONTADO" &&
                model.FormaPago != "CREDITO")
            {
                TempData["ErrorVenta"] =
                    "Seleccione una forma de pago válida.";

                return RedirectToAction(nameof(Crear));
            }

            if (!model.IdCliente.HasValue || model.IdCliente.Value <= 0)
            {
                TempData["ErrorVenta"] =
                    "Seleccione un cliente para la venta.";

                return RedirectToAction(nameof(Crear));
            }

            // El plazo de crédito se define manualmente para cada cuenta.
            // Por eso, una venta a crédito debe incluir su fecha de vencimiento.
            DateTime? fechaVencimiento = model.FechaVencimiento?.Date;

            if (model.FormaPago == "CREDITO")
            {
                if (!fechaVencimiento.HasValue)
                {
                    TempData["ErrorVenta"] =
                        "Debe indicar la fecha de vencimiento de la cuenta por cobrar.";

                    return RedirectToAction(nameof(Crear));
                }

                if (fechaVencimiento.Value < DateTime.Today)
                {
                    TempData["ErrorVenta"] =
                        "La fecha de vencimiento no puede ser anterior al día de hoy.";

                    return RedirectToAction(nameof(Crear));
                }
            }
            else if (fechaVencimiento.HasValue)
            {
                TempData["ErrorVenta"] =
                    "Las ventas de contado no deben incluir una fecha de vencimiento.";

                return RedirectToAction(nameof(Crear));
            }

            try
            {
                using var validacionCliente = _dbConnection.CreateConnection();

                bool clienteValido = validacionCliente.ExecuteScalar<bool>(
                    @"
                    SELECT EXISTS (
                        SELECT 1
                        FROM cliente
                        WHERE id_cliente = @IdCliente
                          AND estado = TRUE
                    );",
                    new { IdCliente = model.IdCliente.Value }
                );

                if (!clienteValido)
                {
                    TempData["ErrorVenta"] =
                        "El cliente seleccionado no existe o está inactivo.";

                    return RedirectToAction(nameof(Crear));
                }
            }
            catch (Exception)
            {
                TempData["ErrorVenta"] =
                    "No fue posible validar el cliente seleccionado.";

                return RedirectToAction(nameof(Crear));
            }

            if (model.DescuentoPorcentaje < 0 ||
                model.DescuentoPorcentaje > 100)
            {
                TempData["ErrorVenta"] =
                    "El descuento debe estar entre 0 y 100 %.";

                return RedirectToAction(nameof(Crear));
            }

            var claimId = User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );

            if (!int.TryParse(claimId, out int idUsuario))
            {
                return Forbid();
            }

            try
            {
                using var connection =
                    _dbConnection.CreateConnection();

                var productosJson = JsonSerializer.Serialize(
                    productos.Select(p => new
                    {
                        IdProducto = p.IdProducto,
                        Cantidad = p.Cantidad
                    })
                );

                var parametros = new DynamicParameters();

                parametros.Add(
                    "p_cliente_id",
                    model.IdCliente.Value,
                    DbType.Int32
                );

                parametros.Add(
                    "p_forma_pago",
                    model.FormaPago,
                    DbType.String
                );

                parametros.Add(
                    "p_usuario_id",
                    idUsuario,
                    DbType.Int32
                );

                parametros.Add(
                    "p_descuento_porcentaje",
                    model.DescuentoPorcentaje,
                    DbType.Decimal
                );

                parametros.Add(
                    "p_productos",
                    productosJson,
                    DbType.String
                );

                parametros.Add(
                    "p_fecha_vencimiento",
                    fechaVencimiento,
                    DbType.Date
                );

                // El procedimiento Sprint 3 registra la venta y,
                // cuando es crédito, crea la cuenta por cobrar.
                var fila = connection.QuerySingle(
                    @"CALL sp_registrar_venta_sprint3(
                        @p_cliente_id,
                        @p_forma_pago,
                        @p_usuario_id,
                        @p_descuento_porcentaje,
                        CAST(@p_productos AS JSON),
                        @p_fecha_vencimiento,
                        NULL::VARCHAR,
                        NULL::INTEGER
                    )",
                    parametros,
                    commandType: CommandType.Text
                );

                string estado =
                    Convert.ToString(fila.o_resultado) ?? string.Empty;

                int? idVenta = fila.o_id_venta == null
                    ? null
                    : Convert.ToInt32(fila.o_id_venta);

                if (estado != "OK" || idVenta == null)
                {
                    TempData["ErrorVenta"] =
                        "No fue posible confirmar la venta.";

                    return RedirectToAction(nameof(Crear));
                }

                TempData["VentaExitosa"] =
                    model.FormaPago == "CREDITO"
                        ? $"La venta a crédito #{idVenta.Value} se registró correctamente y se creó su cuenta por cobrar."
                        : $"La venta #{idVenta.Value} se registró correctamente.";

                TempData["IdVenta"] = idVenta.Value;

                // El envío del comprobante es independiente del registro de la venta.
                // Si el correo falla, la venta permanece registrada.
                try
                {
                    await EnviarComprobanteVentaAsync(
                        connection,
                        idVenta.Value
                    );
                }
                catch (Exception)
                {
                    TempData["AvisoCorreo"] =
                        "La venta se registró correctamente, pero no fue posible " +
                        "enviar el comprobante al correo del cliente.";
                }

                return RedirectToAction(nameof(Crear));
            }
            catch (PostgresException ex)
            {
                TempData["ErrorVenta"] = ex.MessageText;

                return RedirectToAction(nameof(Crear));
            }
            catch (Exception)
            {
                TempData["ErrorVenta"] =
                    "Ocurrió un error al registrar la venta. " +
                    "Verifique la conexión y los procedimientos almacenados.";

                return RedirectToAction(nameof(Crear));
            }

        }

        // =====================================================
        // ENVIAR COMPROBANTE DE COMPRA POR CORREO
        // Utiliza un procedimiento almacenado para obtener
        // los datos de la venta, cliente y productos.
        // =====================================================

        private async Task EnviarComprobanteVentaAsync(
            IDbConnection connection,
            int idVenta)
        {
            var filaDetalle = connection.QuerySingle(
                @"CALL public.sp_obtener_detalle_venta_correo(
                    @p_id_venta,
                    NULL::TEXT
                )",
                new { p_id_venta = idVenta },
                commandType: CommandType.Text
            );

            string? json = Convert.ToString(filaDetalle.o_detalle);

            if (string.IsNullOrWhiteSpace(json) || json == "null")
                throw new InvalidOperationException(
                    "No se encontró el detalle de la venta para generar el comprobante."
                );

            using var documento = JsonDocument.Parse(json);
            JsonElement venta = documento.RootElement;

            string email = ObtenerTexto(venta, "Email");
            if (string.IsNullOrWhiteSpace(email))
                throw new InvalidOperationException(
                    "El cliente no tiene un correo electrónico registrado."
                );

            int numeroVenta = ObtenerEntero(venta, "IdVenta");
            string nombreCliente = WebUtility.HtmlEncode(
                ObtenerTexto(venta, "NombreCliente")
            );
            string formaPago = ObtenerTexto(venta, "FormaPago").ToUpperInvariant();

            decimal subtotal = ObtenerDecimal(venta, "Subtotal");
            decimal descuento = ObtenerDecimal(venta, "Descuento");
            decimal impuesto = ObtenerDecimal(venta, "Impuesto");
            decimal total = ObtenerDecimal(venta, "TotalNeto");
            decimal saldoPendiente = ObtenerDecimal(venta, "SaldoPendiente");

            DateTime fecha = ObtenerFecha(venta, "Fecha");
            DateTime? fechaVencimiento =
                ObtenerFechaNullable(venta, "FechaVencimiento");

            bool esCredito = formaPago == "CREDITO";
            string estadoPago = esCredito
                ? saldoPendiente <= 0 ? "Pagada" : "Pendiente de pago"
                : "Pagada";

            var cultura = CultureInfo.GetCultureInfo("es-CR");
            string moneda(decimal valor) => valor.ToString("C", cultura);

            var filasProductos = new StringBuilder();

            if (venta.TryGetProperty("Productos", out JsonElement productos) &&
                productos.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement producto in productos.EnumerateArray())
                {
                    string codigo = WebUtility.HtmlEncode(
                        ObtenerTexto(producto, "CodigoProducto")
                    );
                    string nombre = WebUtility.HtmlEncode(
                        ObtenerTexto(producto, "NombreProducto")
                    );
                    decimal cantidad = ObtenerDecimal(producto, "Cantidad");
                    decimal precio = ObtenerDecimal(producto, "PrecioUnitario");
                    decimal descuentoProducto = ObtenerDecimal(producto, "Descuento");
                    decimal impuestoProducto = ObtenerDecimal(producto, "Impuesto");
                    decimal subtotalProducto = ObtenerDecimal(producto, "Subtotal");

                    filasProductos.Append($"""
                        <tr>
                            <td style="padding:12px 8px;border-bottom:1px solid #e5e7eb;">
                                <strong>{nombre}</strong><br>
                                <span style="color:#737373;font-size:12px;">{codigo}</span>
                            </td>
                            <td style="padding:12px 8px;text-align:center;border-bottom:1px solid #e5e7eb;">{cantidad:N2}</td>
                            <td style="padding:12px 8px;text-align:right;border-bottom:1px solid #e5e7eb;">{moneda(precio)}</td>
                            <td style="padding:12px 8px;text-align:right;border-bottom:1px solid #e5e7eb;">{moneda(descuentoProducto)}</td>
                            <td style="padding:12px 8px;text-align:right;border-bottom:1px solid #e5e7eb;">{moneda(impuestoProducto)}</td>
                            <td style="padding:12px 8px;text-align:right;border-bottom:1px solid #e5e7eb;"><strong>{moneda(subtotalProducto)}</strong></td>
                        </tr>
                        """);
                }
            }

            if (filasProductos.Length == 0)
                throw new InvalidOperationException(
                    "La venta no contiene productos para generar el comprobante."
                );

            string detallePago = esCredito
                ? $"""
                    <p style="margin:8px 0;">Estado de pago: <strong>{estadoPago}</strong></p>
                    <p style="margin:8px 0;">Saldo pendiente: <strong>{moneda(saldoPendiente)}</strong></p>
                    <p style="margin:8px 0;">Fecha de vencimiento: <strong>{(fechaVencimiento.HasValue ? fechaVencimiento.Value.ToString("dd/MM/yyyy") : "No indicada")}</strong></p>
                    """
                : """
                    <p style="margin:8px 0;">Estado de pago: <strong>Pagada en su totalidad</strong></p>
                    <p style="margin:8px 0;">Saldo pendiente: <strong>₡0,00</strong></p>
                    """;

            string tipoCompra = esCredito
                ? "Confirmación de compra a crédito"
                : "Comprobante de compra - Pagada";

            string cuerpoCorreo = $"""
                <!DOCTYPE html>
                <html lang="es">
                <head>
                    <meta charset="UTF-8">
                    <meta name="viewport" content="width=device-width, initial-scale=1.0">
                </head>
                <body style="margin:0;padding:24px;background:#f5f5f5;font-family:Arial,sans-serif;color:#262626;">
                    <div style="max-width:760px;margin:0 auto;background:#ffffff;border:1px solid #e5e7eb;border-radius:12px;overflow:hidden;">
                        <div style="padding:24px;background:#171717;color:#ffffff;">
                            <h1 style="margin:0;font-size:22px;">Charlie Shop</h1>
                            <p style="margin:8px 0 0;color:#d4d4d4;">{tipoCompra}</p>
                        </div>
                        <div style="padding:24px;">
                            <p>Hola <strong>{nombreCliente}</strong>,</p>
                            <p>Gracias por tu compra. A continuación encontrarás el comprobante con el detalle de tu pedido.</p>

                            <div style="padding:14px;background:#f5f5f5;border-radius:8px;margin:20px 0;">
                                <p style="margin:6px 0;">Número de venta: <strong>#{numeroVenta}</strong></p>
                                <p style="margin:6px 0;">Fecha: <strong>{fecha:dd/MM/yyyy HH:mm}</strong></p>
                                <p style="margin:6px 0;">Forma de pago: <strong>{(esCredito ? "Crédito" : "Contado")}</strong></p>
                            </div>

                            <h3 style="margin:24px 0 10px;">Detalle de productos</h3>
                            <div style="overflow-x:auto;">
                                <table style="width:100%;border-collapse:collapse;font-size:13px;">
                                    <thead>
                                        <tr style="background:#f5f5f5;text-align:left;">
                                            <th style="padding:10px 8px;">Producto</th>
                                            <th style="padding:10px 8px;text-align:center;">Cant.</th>
                                            <th style="padding:10px 8px;text-align:right;">Precio</th>
                                            <th style="padding:10px 8px;text-align:right;">Desc.</th>
                                            <th style="padding:10px 8px;text-align:right;">Impuesto</th>
                                            <th style="padding:10px 8px;text-align:right;">Subtotal</th>
                                        </tr>
                                    </thead>
                                    <tbody>{filasProductos}</tbody>
                                </table>
                            </div>

                            <div style="max-width:340px;margin:20px 0 20px auto;padding:16px;background:#fafafa;border-radius:8px;">
                                <p style="margin:7px 0;">Subtotal: <strong>{moneda(subtotal)}</strong></p>
                                <p style="margin:7px 0;">Descuento: <strong>{moneda(descuento)}</strong></p>
                                <p style="margin:7px 0;">Impuestos: <strong>{moneda(impuesto)}</strong></p>
                                <p style="margin:12px 0 0;padding-top:12px;border-top:1px solid #e5e7eb;font-size:17px;">Total: <strong>{moneda(total)}</strong></p>
                            </div>

                            <div style="padding:16px;background:{(esCredito ? "#fff7ed" : "#f0fdf4")};border-radius:8px;">
                                <h3 style="margin:0 0 10px;">Información de pago</h3>
                                {detallePago}
                            </div>

                            <p style="margin-top:26px;">Gracias por confiar en Charlie Shop.</p>
                            <p>Saludos,<br><strong>Charlie Shop</strong></p>
                        </div>
                        <div style="padding:14px 24px;background:#fafafa;border-top:1px solid #e5e7eb;color:#737373;font-size:12px;">
                            Este correo es un comprobante informativo de compra y no sustituye una factura electrónica tributaria.
                        </div>
                    </div>
                </body>
                </html>
                """;

            string asunto = esCredito
                ? $"Charlie Shop - Compra a crédito #{numeroVenta}"
                : $"Charlie Shop - Comprobante de compra #{numeroVenta}";

            await _emailService.SendAsync(email, asunto, cuerpoCorreo);
        }

        private static string ObtenerTexto(JsonElement elemento, string propiedad)
        {
            if (!elemento.TryGetProperty(propiedad, out JsonElement valor) ||
                valor.ValueKind == JsonValueKind.Null)
                return string.Empty;

            return valor.ValueKind == JsonValueKind.String
                ? valor.GetString() ?? string.Empty
                : valor.ToString();
        }

        private static int ObtenerEntero(JsonElement elemento, string propiedad)
        {
            return elemento.TryGetProperty(propiedad, out JsonElement valor) &&
                   valor.TryGetInt32(out int resultado)
                ? resultado
                : 0;
        }

        private static decimal ObtenerDecimal(JsonElement elemento, string propiedad)
        {
            if (!elemento.TryGetProperty(propiedad, out JsonElement valor) ||
                valor.ValueKind == JsonValueKind.Null)
                return 0m;

            if (valor.ValueKind == JsonValueKind.Number &&
                valor.TryGetDecimal(out decimal numero))
                return numero;

            return decimal.TryParse(
                valor.ToString(),
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out decimal resultado
            ) ? resultado : 0m;
        }

        private static DateTime ObtenerFecha(JsonElement elemento, string propiedad)
        {
            return ObtenerFechaNullable(elemento, propiedad) ?? DateTime.MinValue;
        }

        private static DateTime? ObtenerFechaNullable(JsonElement elemento, string propiedad)
        {
            if (!elemento.TryGetProperty(propiedad, out JsonElement valor) ||
                valor.ValueKind == JsonValueKind.Null ||
                string.IsNullOrWhiteSpace(valor.ToString()))
                return null;

            return DateTime.TryParse(
                valor.ToString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out DateTime fecha
            ) ? fecha : null;
        }
    }
}
