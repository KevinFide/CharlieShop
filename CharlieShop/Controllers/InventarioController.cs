
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using CharlieShop.Data;
using CharlieShop.Models;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace CharlieShop.Controllers
{
    [Authorize]
    public class InventarioController : Controller
    {
        private readonly DbConnection _dbConnection;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public InventarioController(DbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        // DTO tipado para garantizar que todos los campos del panel
        // se serialicen con nombres consistentes y sin depender de DapperRow.
        private sealed class ProductoPanelDto
        {
            public int IdProducto { get; set; }
            public string Codigo { get; set; } = string.Empty;
            public string Nombre { get; set; } = string.Empty;
            public string? Descripcion { get; set; }
            public string? Categoria { get; set; }
            public decimal PrecioVenta { get; set; }
            public decimal? Costo { get; set; }
            public string? UnidadMedida { get; set; }
            public bool Estado { get; set; }
            public decimal StockActual { get; set; }
            public decimal StockMinimo { get; set; }
            public decimal? StockMaximo { get; set; }
            public string? Ubicacion { get; set; }
        }

        // =========================================
        // LISTADO DE INVENTARIO
        // =========================================

        public async Task<IActionResult> Index(
            string? busqueda,
            string estado = "TODOS")
        {
            var modelo = new InventarioIndexViewModel
            {
                Busqueda = busqueda ?? string.Empty,
                Estado = string.IsNullOrWhiteSpace(estado)
                    ? "TODOS"
                    : estado.ToUpperInvariant()
            };

            try
            {
                using var connection = _dbConnection.CreateConnection();

                var parametrosInventario = new DynamicParameters();

                parametrosInventario.Add(
                    "p_busqueda", modelo.Busqueda, DbType.String);

                parametrosInventario.Add(
                    "p_estado", modelo.Estado, DbType.String);

                parametrosInventario.Add(
                    "o_productos",
                    dbType: DbType.String,
                    direction: ParameterDirection.Output);

                await connection.ExecuteAsync(
                    "sp_listar_inventario",
                    parametrosInventario,
                    commandType: CommandType.StoredProcedure);

                // El JSON de sp_listar_inventario debe incluir la propiedad
                // "ImagenUrl" para que se muestre la imagen en la vista.
                modelo.Productos =
                    Deserializar<List<InventarioItemViewModel>>(
                        parametrosInventario.Get<string?>("o_productos"))
                    ?? new List<InventarioItemViewModel>();

                var parametrosResumen = new DynamicParameters();

                parametrosResumen.Add(
                    "o_total_productos",
                    dbType: DbType.Int32,
                    direction: ParameterDirection.Output);

                parametrosResumen.Add(
                    "o_stock_bajo",
                    dbType: DbType.Int32,
                    direction: ParameterDirection.Output);

                parametrosResumen.Add(
                    "o_agotados",
                    dbType: DbType.Int32,
                    direction: ParameterDirection.Output);

                await connection.ExecuteAsync(
                    "sp_obtener_resumen_inventario",
                    parametrosResumen,
                    commandType: CommandType.StoredProcedure);

                modelo.TotalProductos =
                    parametrosResumen.Get<int>("o_total_productos");

                modelo.TotalStockBajo =
                    parametrosResumen.Get<int>("o_stock_bajo");

                modelo.TotalAgotados =
                    parametrosResumen.Get<int>("o_agotados");

                var parametrosStockBajo = new DynamicParameters();

                parametrosStockBajo.Add(
                    "o_productos",
                    dbType: DbType.String,
                    direction: ParameterDirection.Output);

                await connection.ExecuteAsync(
                    "sp_obtener_productos_stock_bajo",
                    parametrosStockBajo,
                    commandType: CommandType.StoredProcedure);

                modelo.ProductosStockBajo =
                    Deserializar<List<InventarioItemViewModel>>(
                        parametrosStockBajo.Get<string?>("o_productos"))
                    ?? new List<InventarioItemViewModel>();

                return View(modelo);
            }
            catch (PostgresException)
            {
                TempData["Error"] =
                    "Ocurrió un error de base de datos al cargar el inventario.";

                return View(modelo);
            }
            catch (NpgsqlException)
            {
                TempData["Error"] =
                    "No fue posible conectar con la base de datos.";

                return View(modelo);
            }
            catch (Exception)
            {
                TempData["Error"] =
                    "Ocurrió un error inesperado.";

                return View(modelo);
            }
        }

        // =========================================
        // HISTORIAL DE MOVIMIENTOS
        // =========================================

        public async Task<IActionResult> Historial(
            int? idProducto,
            string tipoMovimiento = "TODOS",
            DateTime? fechaDesde = null,
            DateTime? fechaHasta = null,
            int pagina = 1,
            int tamanoPagina = 10)
        {
            var modelo = new HistorialInventarioViewModel
            {
                IdProducto = idProducto,
                TipoMovimiento = string.IsNullOrWhiteSpace(tipoMovimiento)
                    ? "TODOS"
                    : tipoMovimiento.ToUpperInvariant(),
                FechaDesde = fechaDesde,
                FechaHasta = fechaHasta,
                Pagina = Math.Max(1, pagina),
                TamanoPagina = tamanoPagina > 0
                    ? Math.Min(tamanoPagina, 100)
                    : 10
            };

            if (modelo.FechaDesde.HasValue &&
                modelo.FechaHasta.HasValue &&
                modelo.FechaDesde.Value.Date > modelo.FechaHasta.Value.Date)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "La fecha inicial no puede ser posterior a la fecha final.");

                modelo.FechaDesde = null;
                modelo.FechaHasta = null;
            }

            try
            {
                using var connection = _dbConnection.CreateConnection();

                modelo.Productos = await ObtenerProductosAsync(connection);

                // Contar movimientos para calcular la paginación.
                var parametrosConteo = CrearParametrosFiltros(modelo);

                parametrosConteo.Add(
                    "o_total",
                    dbType: DbType.Int32,
                    direction: ParameterDirection.Output);

                await connection.ExecuteAsync(
                    "sp_contar_movimientos_inventario",
                    parametrosConteo,
                    commandType: CommandType.StoredProcedure);

                modelo.TotalRegistros =
                    parametrosConteo.Get<int>("o_total");

                // TotalPaginas es de solo lectura:
                // se calcula automáticamente desde TotalRegistros.
                int totalPaginas = modelo.TotalPaginas;

                if (totalPaginas > 0 && modelo.Pagina > totalPaginas)
                {
                    modelo.Pagina = totalPaginas;
                }

                // Obtener los movimientos de la página actual.
                var parametrosMovimientos = CrearParametrosFiltros(modelo);

                parametrosMovimientos.Add(
                    "p_pagina", modelo.Pagina, DbType.Int32);

                parametrosMovimientos.Add(
                    "p_tamano_pagina", modelo.TamanoPagina, DbType.Int32);

                parametrosMovimientos.Add(
                    "o_movimientos",
                    dbType: DbType.String,
                    direction: ParameterDirection.Output);

                await connection.ExecuteAsync(
                    "sp_listar_movimientos_inventario",
                    parametrosMovimientos,
                    commandType: CommandType.StoredProcedure);

                modelo.Movimientos =
                    Deserializar<List<MovimientoInventarioViewModel>>(
                        parametrosMovimientos.Get<string?>("o_movimientos"))
                    ?? new List<MovimientoInventarioViewModel>();

                // TienePaginaAnterior y TienePaginaSiguiente
                // también son de solo lectura y se calculan en el modelo.
                return View(modelo);
            }
            catch (PostgresException)
            {
                TempData["Error"] =
                    "Ocurrió un error de base de datos al cargar el historial.";

                return View(modelo);
            }
            catch (NpgsqlException)
            {
                TempData["Error"] =
                    "No fue posible conectar con la base de datos.";

                return View(modelo);
            }
            catch (Exception)
            {
                TempData["Error"] =
                    "Ocurrió un error inesperado al cargar el historial.";

                return View(modelo);
            }
        }

        // =========================================
        // Crear Producto - GET
        // =========================================
        [HttpGet]
        public IActionResult Crear()
        {
            return View(new CrearProductoViewModel());
        }

        // =========================================
        // Crear Producto - GET
        // =========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Crear(CrearProductoViewModel modelo)
        {
            if (modelo.StockInicial < 0)
                ModelState.AddModelError(nameof(modelo.StockInicial), "El stock inicial no puede ser negativo.");

            if (!ModelState.IsValid)
                return View(modelo);

            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out int idUsuario))
            {
                ModelState.AddModelError(string.Empty, "No fue posible identificar al usuario de la sesión.");
                return View(modelo);
            }

            try
            {
                using var connection = (NpgsqlConnection)_dbConnection.CreateConnection();
                connection.Open();

                using var command = new NpgsqlCommand(
                    "CALL public.sp_crear_producto(@p_codigo, @p_nombre, @p_descripcion, @p_categoria, @p_precio_venta, @p_costo, @p_unidad_medida, @p_stock_inicial, @p_usuario_id, NULL);",
                    connection);

                command.Parameters.AddWithValue("p_codigo", modelo.Codigo.Trim());
                command.Parameters.AddWithValue("p_nombre", modelo.Nombre.Trim());
                command.Parameters.AddWithValue("p_descripcion", (object?)modelo.Descripcion ?? DBNull.Value);
                command.Parameters.AddWithValue("p_categoria", (object?)modelo.Categoria ?? DBNull.Value);
                command.Parameters.AddWithValue("p_precio_venta", modelo.PrecioVenta);
                command.Parameters.AddWithValue("p_costo", (object?)modelo.Costo ?? DBNull.Value);
                command.Parameters.AddWithValue("p_unidad_medida", (object?)modelo.UnidadMedida ?? DBNull.Value);
                command.Parameters.AddWithValue("p_stock_inicial", modelo.StockInicial);
                command.Parameters.AddWithValue("p_usuario_id", idUsuario);

                var resultado = command.ExecuteScalar()?.ToString();

                if (resultado == "CREADO")
                {
                    TempData["Exito"] = "El producto y su inventario fueron registrados correctamente.";
                    return RedirectToAction(nameof(Index));
                }

                ModelState.AddModelError(string.Empty, resultado switch
                {
                    "CODIGO_EXISTENTE" => "El código del producto ya existe.",
                    "STOCK_INVALIDO" => "El stock inicial no puede ser negativo.",
                    "USUARIO_INVALIDO" => "El usuario de la sesión no es válido.",
                    _ => resultado ?? "No fue posible registrar el producto y su inventario."
                });
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty,
                    "No fue posible registrar el producto. Verificá la base de datos e intentá nuevamente.");
            }

            return View(modelo);
        }

        // =========================================
        // EDICIÓN DE PRODUCTO DESDE EL PANEL LATERAL
        // =========================================

        [HttpGet]
        public async Task<IActionResult> ObtenerProducto(int id)
        {
            if (id <= 0)
                return BadRequest(new { success = false, message = "Identificador de producto inválido." });

            try
            {
                using var connection = _dbConnection.CreateConnection();
                const string sql = @"
                    SELECT
                        p.id_producto AS IdProducto,
                        p.codigo AS Codigo,
                        p.nombre AS Nombre,
                        p.descripcion AS Descripcion,
                        p.categoria AS Categoria,
                        p.precio_venta AS PrecioVenta,
                        p.costo AS Costo,
                        p.unidad_medida AS UnidadMedida,
                        p.estado AS Estado,
                        COALESCE(i.stock_actual, 0) AS StockActual,
                        COALESCE(i.stock_minimo, 0) AS StockMinimo,
                        i.stock_maximo AS StockMaximo,
                        i.ubicacion AS Ubicacion
                    FROM public.producto p
                    LEFT JOIN public.inventario i
                        ON i.producto_id = p.id_producto
                    WHERE p.id_producto = @IdProducto;";

                var producto = await connection.QueryFirstOrDefaultAsync<ProductoPanelDto>(
                    sql,
                    new { IdProducto = id });

                if (producto == null)
                    return NotFound(new { success = false, message = "El producto no existe." });

                return Json(new { success = true, producto });
            }
            catch (Exception)
            {
                return StatusCode(500, new { success = false, message = "No fue posible cargar los datos del producto." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarEdicion(EditarProductoViewModel modelo)
        {
            if (!ModelState.IsValid)
            {
                var errores = ModelState.Values.SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage).Where(e => !string.IsNullOrWhiteSpace(e)).ToList();
                return BadRequest(new { success = false, message = errores.FirstOrDefault() ?? "Revisá los datos ingresados." });
            }

            try
            {
                await using var connection = (NpgsqlConnection)_dbConnection.CreateConnection();
                await connection.OpenAsync();
                await using var command = new NpgsqlCommand(
                    "CALL public.sp_editar_producto(@p_id_producto, @p_codigo, @p_nombre, @p_descripcion, @p_categoria, @p_precio_venta, @p_costo, @p_unidad_medida, NULL);",
                    connection);

                command.Parameters.AddWithValue("p_id_producto", modelo.IdProducto);
                command.Parameters.AddWithValue("p_codigo", modelo.Codigo.Trim());
                command.Parameters.AddWithValue("p_nombre", modelo.Nombre.Trim());
                command.Parameters.AddWithValue("p_descripcion", (object?)modelo.Descripcion ?? DBNull.Value);
                command.Parameters.AddWithValue("p_categoria", (object?)modelo.Categoria ?? DBNull.Value);
                command.Parameters.AddWithValue("p_precio_venta", modelo.PrecioVenta);
                command.Parameters.AddWithValue("p_costo", (object?)modelo.Costo ?? DBNull.Value);
                command.Parameters.AddWithValue("p_unidad_medida", (object?)modelo.UnidadMedida ?? DBNull.Value);

                var resultado = (await command.ExecuteScalarAsync())?.ToString();
                return resultado == "ACTUALIZADO"
                    ? Json(new { success = true, message = "Los datos del producto se actualizaron correctamente." })
                    : BadRequest(new
                    {
                        success = false,
                        message = resultado switch
                        {
                            "NO_ENCONTRADO" => "El producto ya no existe.",
                            "CODIGO_EXISTENTE" => "Ya existe otro producto con ese código.",
                            _ => resultado ?? "No fue posible actualizar el producto."
                        }
                    });
            }
            catch (PostgresException ex) when (ex.SqlState == "23505")
            {
                return BadRequest(new { success = false, message = "Ya existe un producto con ese código." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { success = false, message = "Ocurrió un error al guardar los cambios." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarEstadoProducto(int idProducto, bool estado)
        {
            if (idProducto <= 0)
                return BadRequest(new { success = false, message = "Identificador de producto inválido." });

            try
            {
                await using var connection = (NpgsqlConnection)_dbConnection.CreateConnection();
                await connection.OpenAsync();
                await using var command = new NpgsqlCommand(
                    "CALL public.sp_cambiar_estado_producto(@p_id_producto, @p_estado, NULL);", connection);
                command.Parameters.AddWithValue("p_id_producto", idProducto);
                command.Parameters.AddWithValue("p_estado", estado);
                var resultado = (await command.ExecuteScalarAsync())?.ToString();

                return resultado == (estado ? "ACTIVADO" : "DESACTIVADO")
                    ? Json(new { success = true, estado, message = estado ? "Producto activado." : "Producto desactivado." })
                    : BadRequest(new { success = false, message = resultado == "PRODUCTO_NO_ENCONTRADO" ? "El producto no existe." : "No fue posible cambiar el estado." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { success = false, message = "Ocurrió un error al cambiar el estado del producto." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistrarMovimientoPanel(
            int idProducto, string tipoMovimiento, decimal cantidad,
            string? referencia, string? observaciones)
        {
            tipoMovimiento = (tipoMovimiento ?? string.Empty).Trim().ToUpperInvariant();
            if (idProducto <= 0 || cantidad <= 0 ||
                (tipoMovimiento != "ENTRADA" && tipoMovimiento != "SALIDA"))
                return BadRequest(new { success = false, message = "Verificá el tipo de movimiento y que la cantidad sea mayor que cero." });

            if (!ObtenerIdUsuario(out int idUsuario))
                return Unauthorized(new { success = false, message = "No fue posible identificar al usuario de la sesión." });

            try
            {
                await using var connection = (NpgsqlConnection)_dbConnection.CreateConnection();
                await connection.OpenAsync();
                await using var command = new NpgsqlCommand(
                    "CALL public.sp_ajustar_inventario(@p_id_producto, @p_direccion, @p_cantidad, @p_referencia, @p_observaciones, @p_usuario_id, NULL);",
                    connection);
                command.Parameters.AddWithValue("p_id_producto", idProducto);
                command.Parameters.AddWithValue("p_direccion", tipoMovimiento);
                command.Parameters.AddWithValue("p_cantidad", cantidad);
                command.Parameters.AddWithValue("p_referencia", (object?)referencia ?? DBNull.Value);
                command.Parameters.AddWithValue("p_observaciones", (object?)observaciones ?? DBNull.Value);
                command.Parameters.AddWithValue("p_usuario_id", idUsuario);

                var resultado = (await command.ExecuteScalarAsync())?.ToString();
                if (resultado == "OK" || resultado == "AJUSTADO")
                    return Json(new { success = true, message = tipoMovimiento == "ENTRADA" ? "Entrada registrada correctamente." : "Salida registrada correctamente." });

                var mensaje = resultado switch
                {
                    "CANTIDAD_INVALIDA" => "La cantidad debe ser mayor que cero.",
                    "DIRECCION_INVALIDA" => "El tipo de movimiento no es válido.",
                    "USUARIO_INVALIDO" => "El usuario no es válido o está inactivo.",
                    "PRODUCTO_NO_ENCONTRADO" => "El producto no existe o está inactivo.",
                    "INVENTARIO_NO_ENCONTRADO" => "No existe inventario para realizar la salida.",
                    "STOCK_INSUFICIENTE" => "No hay existencias suficientes para realizar esa salida.",
                    _ => resultado ?? "No fue posible registrar el movimiento."
                };
                return BadRequest(new { success = false, message = mensaje });
            }
            catch (Exception)
            {
                return StatusCode(500, new { success = false, message = "Ocurrió un error al registrar el movimiento." });
            }
        }

        // =========================================
        // ELIMINAR PRODUCTO DESDE INVENTARIO
        // =========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarProducto(
            int id,
            string? nombreProducto)
        {
            void SetDeleteResult(string tipo, string mensaje)
            {
                TempData["EliminarTipo"] = tipo;
                TempData["EliminarMensaje"] = mensaje;
                TempData["EliminarNombre"] = nombreProducto;
            }

            if (id <= 0)
            {
                SetDeleteResult(
                    "error",
                    "El identificador del producto no es válido. Actualizá la página e intentá nuevamente.");
                return RedirectToAction(nameof(Index));
            }

            try
            {
                await using var connection =
                    (NpgsqlConnection)_dbConnection.CreateConnection();

                await connection.OpenAsync();

                await using var command = new NpgsqlCommand(
                    "CALL public.sp_eliminar_producto(@p_id_producto, NULL);",
                    connection);

                command.Parameters.AddWithValue("p_id_producto", id);

                var resultado =
                    (await command.ExecuteScalarAsync())?.ToString()?.Trim();

                switch (resultado)
                {
                    case "ELIMINADO":
                        SetDeleteResult(
                            "success",
                            "El producto fue eliminado correctamente.");
                        break;

                    case "NO_ENCONTRADO":
                        SetDeleteResult(
                            "error",
                            "El producto no fue encontrado. Es posible que ya haya sido eliminado.");
                        break;

                    case "TIENE_VENTAS":
                        SetDeleteResult(
                            "warning",
                            "Este producto tiene ventas asociadas. No se puede eliminar porque debe conservarse el historial de ventas.");
                        break;

                    case "TIENE_MOVIMIENTOS":
                        SetDeleteResult(
                            "warning",
                            "Este producto tiene movimientos de inventario asociados. No se puede eliminar porque debe conservarse el historial de existencias.");
                        break;

                    case null:
                    case "":
                        SetDeleteResult(
                            "error",
                            "La base de datos no devolvió un resultado. No se confirmó la eliminación del producto.");
                        break;

                    default:
                        SetDeleteResult(
                            "error",
                            "No fue posible eliminar el producto. Verificá sus registros relacionados e intentá nuevamente.");
                        break;
                }
            }
            catch (PostgresException ex) when (ex.SqlState == "23503")
            {
                SetDeleteResult(
                    "warning",
                    "El producto tiene registros relacionados en el sistema. No se puede eliminar para proteger la información histórica.");
            }
            catch (PostgresException)
            {
                SetDeleteResult(
                    "error",
                    "Ocurrió un error en la base de datos al intentar eliminar el producto.");
            }
            catch (NpgsqlException)
            {
                SetDeleteResult(
                    "error",
                    "No fue posible conectar con la base de datos. Revisá la conexión e intentá nuevamente.");
            }
            catch (Exception)
            {
                SetDeleteResult(
                    "error",
                    "Ocurrió un error inesperado al intentar eliminar el producto.");
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================================
        // MÉTODOS AUXILIARES
        // =========================================

        private static DynamicParameters CrearParametrosFiltros(
            HistorialInventarioViewModel modelo)
        {
            var parametros = new DynamicParameters();

            parametros.Add(
                "p_id_producto", modelo.IdProducto, DbType.Int32);

            parametros.Add(
                "p_tipo_movimiento", modelo.TipoMovimiento, DbType.String);

            parametros.Add(
                "p_fecha_desde", modelo.FechaDesde?.Date, DbType.Date);

            parametros.Add(
                "p_fecha_hasta", modelo.FechaHasta?.Date, DbType.Date);

            return parametros;
        }

        // Se usa IDbConnection en lugar de dynamic.
        private static async Task<List<ProductoInventarioOpcionViewModel>>
            ObtenerProductosAsync(IDbConnection connection)
        {
            var parametros = new DynamicParameters();

            parametros.Add(
                "o_productos",
                dbType: DbType.String,
                direction: ParameterDirection.Output);

            await connection.ExecuteAsync(
                "sp_obtener_productos_para_inventario",
                parametros,
                commandType: CommandType.StoredProcedure);

            string? json = parametros.Get<string?>("o_productos");

            return Deserializar<List<ProductoInventarioOpcionViewModel>>(json)
                ?? new List<ProductoInventarioOpcionViewModel>();
        }

        private bool ObtenerIdUsuario(out int idUsuario)
        {
            string? valor = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(valor, out idUsuario);
        }

        private static T? Deserializar<T>(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
    }
}
