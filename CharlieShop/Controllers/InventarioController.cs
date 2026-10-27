
using System;
using System.Collections.Generic;
using System.Data;
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
        // REGISTRAR MOVIMIENTO - GET
        // =========================================

        [HttpGet]
        public async Task<IActionResult> RegistrarMovimiento(int? idProducto)
        {
            var modelo = new RegistrarMovimientoViewModel
            {
                IdProducto = idProducto ?? 0,
                TipoMovimiento = "ENTRADA"
            };

            try
            {
                using var connection = _dbConnection.CreateConnection();
                modelo.Productos = await ObtenerProductosAsync(connection);

                return View(modelo);
            }
            catch (Exception)
            {
                TempData["Error"] = "No fue posible cargar los productos.";
                return RedirectToAction(nameof(Index));
            }
        }

        // =========================================
        // REGISTRAR MOVIMIENTO - POST
        // =========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistrarMovimiento(
            RegistrarMovimientoViewModel modelo)
        {
            // Validar cantidad en el servidor.
            if (modelo.Cantidad <= 0)
            {
                ModelState.AddModelError(
                    nameof(modelo.Cantidad),
                    "La cantidad debe ser mayor que cero.");
            }

            modelo.TipoMovimiento = (modelo.TipoMovimiento ?? string.Empty)
                .Trim()
                .ToUpperInvariant();

            if (modelo.TipoMovimiento != "ENTRADA" &&
                modelo.TipoMovimiento != "SALIDA")
            {
                ModelState.AddModelError(
                    nameof(modelo.TipoMovimiento),
                    "El tipo de movimiento debe ser ENTRADA o SALIDA.");
            }

            if (!ModelState.IsValid)
            {
                await CargarProductosAsync(modelo);
                return View(modelo);
            }

            if (!ObtenerIdUsuario(out int idUsuario))
            {
                TempData["Error"] =
                    "No fue posible identificar al usuario de la sesión.";

                await CargarProductosAsync(modelo);
                return View(modelo);
            }

            try
            {
                using var connection = _dbConnection.CreateConnection();

                var parametros = new DynamicParameters();

                parametros.Add("p_id_producto", modelo.IdProducto, DbType.Int32);
                parametros.Add("p_direccion", modelo.TipoMovimiento, DbType.String);
                parametros.Add("p_cantidad", modelo.Cantidad, DbType.Decimal);
                parametros.Add("p_referencia", modelo.Referencia, DbType.String);
                parametros.Add("p_observaciones", modelo.Observaciones, DbType.String);
                parametros.Add("p_usuario_id", idUsuario, DbType.Int32);

                parametros.Add(
                    "o_resultado",
                    dbType: DbType.String,
                    direction: ParameterDirection.Output);

                await connection.ExecuteAsync(
                    "sp_ajustar_inventario",
                    parametros,
                    commandType: CommandType.StoredProcedure);

                string? resultado = parametros.Get<string?>("o_resultado");

                if (resultado == "OK" || resultado == "AJUSTADO")
                {
                    TempData["Exito"] = modelo.TipoMovimiento == "ENTRADA"
                        ? "La entrada de inventario se registró correctamente."
                        : "La salida de inventario se registró correctamente.";

                    return RedirectToAction(nameof(Index));
                }

                TempData["Error"] = resultado switch
                {
                    "CANTIDAD_INVALIDA" =>
                        "La cantidad debe ser mayor que cero.",

                    "DIRECCION_INVALIDA" =>
                        "El tipo de movimiento seleccionado no es válido.",

                    "USUARIO_INVALIDO" =>
                        "El usuario no es válido o está inactivo.",

                    "PRODUCTO_NO_ENCONTRADO" =>
                        "El producto no existe o está inactivo.",

                    "INVENTARIO_NO_ENCONTRADO" =>
                        "No existe un registro de inventario para realizar la salida.",

                    "STOCK_INSUFICIENTE" =>
                        "No hay existencias suficientes para realizar esa salida.",

                    _ when resultado?.StartsWith("Error:", StringComparison.OrdinalIgnoreCase) == true =>
                        resultado,

                    _ =>
                        "No fue posible registrar el movimiento de inventario."
                };
            }
            catch (PostgresException)
            {
                TempData["Error"] =
                    "Ocurrió un error de base de datos al registrar el movimiento.";
            }
            catch (NpgsqlException)
            {
                TempData["Error"] =
                    "No fue posible conectar con la base de datos.";
            }
            catch (Exception)
            {
                TempData["Error"] =
                    "Ocurrió un error inesperado al registrar el movimiento.";
            }

            await CargarProductosAsync(modelo);
            return View(modelo);
        }

        // =========================================
        // AJUSTAR INVENTARIO - GET
        // =========================================

        [HttpGet]
        public async Task<IActionResult> AjustarInventario()
        {
            var modelo = new AjustarInventarioViewModel();

            try
            {
                using var connection = _dbConnection.CreateConnection();
                modelo.Productos = await ObtenerProductosAsync(connection);

                return View(modelo);
            }
            catch (Exception)
            {
                TempData["Error"] = "No fue posible cargar los productos.";
                return RedirectToAction(nameof(Index));
            }
        }

        // =========================================
        // AJUSTAR INVENTARIO - POST
        // =========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AjustarInventario(
            AjustarInventarioViewModel modelo)
        {
            // Validar la cantidad en el servidor sin usar Range con decimales.
            if (modelo.Cantidad <= 0)
            {
                ModelState.AddModelError(
                    nameof(modelo.Cantidad),
                    "La cantidad debe ser mayor que cero.");
            }

            if (!ModelState.IsValid)
            {
                await CargarProductosAsync(modelo);
                return View(modelo);
            }

            modelo.Direccion = (modelo.Direccion ?? string.Empty)
                .Trim()
                .ToUpperInvariant();

            if (modelo.Direccion != "ENTRADA" &&
                modelo.Direccion != "SALIDA")
            {
                ModelState.AddModelError(
                    nameof(modelo.Direccion),
                    "La dirección debe ser ENTRADA o SALIDA.");

                await CargarProductosAsync(modelo);
                return View(modelo);
            }

            if (!ObtenerIdUsuario(out int idUsuario))
            {
                TempData["Error"] =
                    "No fue posible identificar al usuario de la sesión.";

                await CargarProductosAsync(modelo);
                return View(modelo);
            }

            try
            {
                using var connection = _dbConnection.CreateConnection();

                var parametros = new DynamicParameters();

                parametros.Add("p_id_producto", modelo.IdProducto, DbType.Int32);
                parametros.Add("p_direccion", modelo.Direccion, DbType.String);
                parametros.Add("p_cantidad", modelo.Cantidad, DbType.Decimal);
                parametros.Add("p_referencia", modelo.Referencia, DbType.String);
                parametros.Add("p_observaciones", modelo.Observaciones, DbType.String);
                parametros.Add("p_usuario_id", idUsuario, DbType.Int32);

                parametros.Add(
                    "o_resultado",
                    dbType: DbType.String,
                    direction: ParameterDirection.Output);

                await connection.ExecuteAsync(
                    "sp_ajustar_inventario",
                    parametros,
                    commandType: CommandType.StoredProcedure);

                string? resultado = parametros.Get<string?>("o_resultado");

                if (resultado == "AJUSTADO" || resultado == "OK")
                {
                    TempData["Exito"] =
                        "El ajuste de inventario se realizó correctamente.";

                    return RedirectToAction(nameof(Index));
                }

                TempData["Error"] = resultado switch
                {
                    "CANTIDAD_INVALIDA" =>
                        "La cantidad debe ser mayor que cero.",

                    "DIRECCION_INVALIDA" =>
                        "La dirección seleccionada no es válida.",

                    "USUARIO_INVALIDO" =>
                        "El usuario no es válido o está inactivo.",

                    "PRODUCTO_NO_ENCONTRADO" =>
                        "El producto no existe o está inactivo.",

                    "INVENTARIO_NO_ENCONTRADO" =>
                        "No existe un registro de inventario para realizar la salida.",

                    "STOCK_INSUFICIENTE" =>
                        "No hay existencias suficientes para realizar esa salida.",

                    _ =>
                        "No fue posible realizar el ajuste de inventario."
                };
            }
            catch (PostgresException)
            {
                TempData["Error"] =
                    "Ocurrió un error de base de datos al ajustar el inventario.";
            }
            catch (NpgsqlException)
            {
                TempData["Error"] =
                    "No fue posible conectar con la base de datos.";
            }
            catch (Exception)
            {
                TempData["Error"] =
                    "Ocurrió un error inesperado al ajustar el inventario.";
            }

            await CargarProductosAsync(modelo);
            return View(modelo);
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

        private async Task CargarProductosAsync(
            RegistrarMovimientoViewModel modelo)
        {
            try
            {
                using var connection = _dbConnection.CreateConnection();
                modelo.Productos = await ObtenerProductosAsync(connection);
            }
            catch (Exception)
            {
                modelo.Productos = new List<ProductoInventarioOpcionViewModel>();
            }
        }

        private async Task CargarProductosAsync(
            AjustarInventarioViewModel modelo)
        {
            try
            {
                using var connection = _dbConnection.CreateConnection();
                modelo.Productos = await ObtenerProductosAsync(connection);
            }
            catch (Exception)
            {
                modelo.Productos = new List<ProductoInventarioOpcionViewModel>();
            }
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
