
using System.Data;
using System.Security.Claims;
using System.Text.Json;
using CharlieShop.Data;
using CharlieShop.Models;
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

        private readonly JsonSerializerOptions _jsonOptions =
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

        public VentasController(DbConnection dbConnection)
        {
            _dbConnection = dbConnection;
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

                // Consultar la información general de la venta.
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

                // Si la venta no existe, informar al usuario.
                if (venta == null)
                {
                    return NotFound(
                        $"La venta #{id} no fue encontrada."
                    );
                }

                // Consultar los productos asociados a la venta.
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

                var clientes = connection.Query(sql, new { Busqueda = busqueda?.Trim() ?? string.Empty })
                    .Select(c => new
                    {
                        idCliente = (int)c.IdCliente,
                        nombre = (string)c.Nombre,
                        apellido = (string)c.Apellido,
                        identificacion = (string?)c.Identificacion
                    }).ToList();

                return Json(clientes);
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { mensaje = "No fue posible consultar los clientes." });
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
        public IActionResult Finalizar(CrearVentaViewModel model)
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

            // Validar carrito vacío.
            if (productos.Count == 0)
            {
                TempData["ErrorVenta"] =
                    "Debe agregar al menos un producto a la venta.";

                return RedirectToAction(nameof(Crear));
            }

            // Validar identificadores y cantidades.
            if (productos.Any(p =>
                p.IdProducto <= 0 ||
                p.Cantidad <= 0 ||
                p.Cantidad != decimal.Round(p.Cantidad, 2)))
            {
                TempData["ErrorVenta"] =
                    "Verifique que los productos tengan una cantidad válida mayor que cero.";

                return RedirectToAction(nameof(Crear));
            }

            // El SP espera una línea por producto.
            if (productos.GroupBy(p => p.IdProducto)
                         .Any(g => g.Count() > 1))
            {
                TempData["ErrorVenta"] =
                    "Hay productos repetidos en el carrito.";

                return RedirectToAction(nameof(Crear));
            }

            // Validar forma de pago.
            if (model.FormaPago != "CONTADO" &&
                model.FormaPago != "CREDITO")
            {
                TempData["ErrorVenta"] =
                    "Seleccione una forma de pago válida.";

                return RedirectToAction(nameof(Crear));
            }

            // Validar que el cliente exista y esté activo.
            if (model.IdCliente <= 0)
            {
                TempData["ErrorVenta"] = "Seleccione un cliente para la venta.";
                return RedirectToAction(nameof(Crear));
            }

            try
            {
                using var validacionCliente = _dbConnection.CreateConnection();
                bool clienteValido = validacionCliente.ExecuteScalar<bool>(
                    "SELECT EXISTS (SELECT 1 FROM cliente WHERE id_cliente = @IdCliente AND estado = TRUE);",
                    new { model.IdCliente });
                if (!clienteValido)
                {
                    TempData["ErrorVenta"] = "El cliente seleccionado no existe o está inactivo.";
                    return RedirectToAction(nameof(Crear));
                }
            }
            catch (Exception)
            {
                TempData["ErrorVenta"] = "No fue posible validar el cliente seleccionado.";
                return RedirectToAction(nameof(Crear));
            }

            // Validar descuento.
            if (model.DescuentoPorcentaje < 0 ||
                model.DescuentoPorcentaje > 100)
            {
                TempData["ErrorVenta"] =
                    "El descuento debe estar entre 0 y 100 %.";

                return RedirectToAction(nameof(Crear));
            }

            // Obtener el usuario autenticado.
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

                // PostgreSQL consulta los precios y valida las existencias.
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
                    model.IdCliente,
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

                var fila = connection.QuerySingle(
                    @"CALL sp_registrar_venta(
                        @p_cliente_id,
                        @p_forma_pago,
                        @p_usuario_id,
                        @p_descuento_porcentaje,
                        CAST(@p_productos AS JSON),
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
                    $"La venta #{idVenta.Value} se registró correctamente.";

                TempData["IdVenta"] = idVenta.Value;

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
    }
}
