using System.Data;
using System.Text.Json;
using CharlieShop.Data;
using CharlieShop.Models;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace CharlieShop.Controllers
{
    [Authorize(Roles = "ADMINISTRADOR")]
    public class ReportesController : Controller
    {
        private readonly DbConnection _dbConnection;

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public ReportesController(DbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        [HttpGet]
        public IActionResult Index(
            string? reporte = "analisis",
            DateTime? fechaInicio = null,
            DateTime? fechaFin = null)
        {
            var model = new ReportesViewModel
            {
                ReporteActual = NormalizarReporte(reporte),
                FechaInicio = fechaInicio?.Date ?? DateTime.Today.AddDays(-29),
                FechaFin = fechaFin?.Date ?? DateTime.Today
            };

            if (model.FechaInicio > model.FechaFin &&
                (model.ReporteActual == "analisis" || model.ReporteActual == "ventas"))
            {
                model.Error = "La fecha inicial no puede ser mayor que la fecha final.";
                return View(model);
            }

            try
            {
                using var connection = _dbConnection.CreateConnection();

                switch (model.ReporteActual)
                {
                    case "analisis":
                        model.AnalisisGeneral = EjecutarProcedimientoObjeto<ReporteAnalisisGeneral>(
                            connection,
                            @"CALL public.sp_obtener_analisis_general(
                                CAST(@p_fecha_inicio AS DATE),
                                CAST(@p_fecha_fin AS DATE),
                                NULL
                            )",
                            new
                            {
                                p_fecha_inicio = model.FechaInicio,
                                p_fecha_fin = model.FechaFin
                            });
                        break;

                    case "ventas":
                        model.Ventas = EjecutarProcedimiento<ReporteVentaItem>(
                            connection,
                            @"CALL public.sp_reporte_ventas(
                                CAST(@p_fecha_inicio AS DATE),
                                CAST(@p_fecha_fin AS DATE),
                                NULL
                            )",
                            new
                            {
                                p_fecha_inicio = model.FechaInicio,
                                p_fecha_fin = model.FechaFin
                            });
                        if (!model.Ventas.Any())
                            model.Mensaje = "No se encontraron ventas para el período seleccionado.";
                        break;

                    case "productos":
                        model.ProductosMasVendidos = EjecutarProcedimiento<ReporteProductoVendidoItem>(
                            connection,
                            "CALL public.sp_reporte_productos_mas_vendidos(NULL)",
                            new { });
                        if (!model.ProductosMasVendidos.Any())
                            model.Mensaje = "Todavía no hay productos vendidos para mostrar.";
                        break;

                    case "inventario":
                        model.Inventario = EjecutarProcedimiento<ReporteInventarioItem>(
                            connection,
                            "CALL public.sp_reporte_inventario(NULL)",
                            new { });
                        if (!model.Inventario.Any())
                            model.Mensaje = "No hay productos registrados en el inventario.";
                        break;

                    case "cuentas":
                        model.CuentasPorCobrar = EjecutarProcedimiento<ReporteCuentaPorCobrarItem>(
                            connection,
                            "CALL public.sp_reporte_cuentas_por_cobrar(NULL)",
                            new { });
                        if (!model.CuentasPorCobrar.Any())
                            model.Mensaje = "No existen cuentas con saldos pendientes.";
                        break;

                    case "clientes":
                        model.Clientes = EjecutarProcedimiento<ReporteClienteItem>(
                            connection,
                            "CALL public.sp_reporte_clientes(NULL)",
                            new { });
                        if (!model.Clientes.Any())
                            model.Mensaje = "No hay clientes registrados para mostrar.";
                        break;
                }
            }
            catch (PostgresException)
            {
                model.Error = "No fue posible generar el reporte. Verifique que los procedimientos almacenados estén creados y actualizados.";
            }
            catch (NpgsqlException)
            {
                model.Error = "No fue posible conectar con la base de datos.";
            }
            catch (Exception)
            {
                model.Error = "Ocurrió un error inesperado al generar el reporte.";
            }

            return View(model);
        }

        // Compatibilidad con las rutas anteriores del módulo.
        [HttpGet]
        public IActionResult Inventario() =>
            RedirectToAction(nameof(Index), new { reporte = "inventario" });

        [HttpGet]
        public IActionResult Clientes() =>
            RedirectToAction(nameof(Index), new { reporte = "clientes" });

        private List<T> EjecutarProcedimiento<T>(
            IDbConnection connection,
            string llamada,
            object parametros)
        {
            var fila = connection.QuerySingle<string>(
                llamada,
                parametros,
                commandType: CommandType.Text
            );

            if (string.IsNullOrWhiteSpace(fila))
                return new List<T>();

            return JsonSerializer.Deserialize<List<T>>(fila, _jsonOptions)
                ?? new List<T>();
        }

        private T EjecutarProcedimientoObjeto<T>(
            IDbConnection connection,
            string llamada,
            object parametros) where T : new()
        {
            var fila = connection.QuerySingle<string>(
                llamada,
                parametros,
                commandType: CommandType.Text
            );

            if (string.IsNullOrWhiteSpace(fila))
                return new T();

            return JsonSerializer.Deserialize<T>(fila, _jsonOptions) ?? new T();
        }

        private static string NormalizarReporte(string? reporte)
        {
            var valor = (reporte ?? "analisis").Trim().ToLowerInvariant();

            return valor switch
            {
                "analisis" => "analisis",
                "ventas" => "ventas",
                "productos" => "productos",
                "inventario" => "inventario",
                "cuentas" => "cuentas",
                "clientes" => "clientes",
                _ => "analisis"
            };
        }
    }
}
