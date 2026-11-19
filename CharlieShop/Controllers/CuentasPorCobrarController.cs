
using CharlieShop.Data;
using CharlieShop.Models;
using CharlieShop.Services;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Data;
using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text.Json;

namespace CharlieShop.Controllers
{
    [Authorize]
    public class CuentasPorCobrarController : Controller
    {
        private readonly DbConnection _dbConnection;
        private readonly EmailService _emailService;

        private readonly JsonSerializerOptions _jsonOptions =
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

        public CuentasPorCobrarController(
            DbConnection dbConnection,
            EmailService emailService)
        {
            _dbConnection = dbConnection;
            _emailService = emailService;
        }

        // =====================================================
        // HU-38: LISTAR CUENTAS PENDIENTES
        // HU-39: BUSCAR CUENTAS POR CLIENTE
        // HU-41: FILTRAR CUENTAS POR ESTADO
        // =====================================================

        [HttpGet]
        public IActionResult Index(
            string? busqueda,
            string? estadoFiltro)
        {
            var model = new CuentaPorCobrarViewModel
            {
                Busqueda = busqueda?.Trim(),
                EstadoFiltro = (estadoFiltro ?? "TODOS")
                    .Trim()
                    .ToUpperInvariant()
            };

            string[] estadosPermitidos =
            {
                "TODOS",
                "PENDIENTE",
                "PARCIAL",
                "VENCIDA",
                "PAGADA"
            };

            if (!estadosPermitidos.Contains(model.EstadoFiltro))
            {
                model.EstadoFiltro = "TODOS";
            }

            try
            {
                using var connection =
                    _dbConnection.CreateConnection();

                var fila = connection.QuerySingle(
                    @"CALL public.sp_listar_cuentas_por_cobrar(
                        @p_busqueda,
                        @p_estado,
                        NULL::TEXT
                    )",
                    new
                    {
                        p_busqueda = model.Busqueda,
                        p_estado = model.EstadoFiltro
                    },
                    commandType: CommandType.Text
                );

                string? json = Convert.ToString(fila.o_cuentas);

                var cuentas = DeserializarLista<
                    CuentaPorCobrarViewModel>(json);

                ViewBag.Cuentas = cuentas;

                return View(model);
            }
            catch (PostgresException)
            {
                ViewBag.ErrorCuentas =
                    "No fue posible consultar las cuentas por cobrar. " +
                    "Verifique el procedimiento almacenado y la conexión.";

                ViewBag.Cuentas =
                    new List<CuentaPorCobrarViewModel>();

                return View(model);
            }
            catch (Exception)
            {
                ViewBag.ErrorCuentas =
                    "Ocurrió un error al cargar las cuentas por cobrar.";

                ViewBag.Cuentas =
                    new List<CuentaPorCobrarViewModel>();

                return View(model);
            }
        }

        // =====================================================
        // HU-39: BUSCAR CUENTAS POR CLIENTE
        // Utiliza el procedimiento específico de búsqueda.
        // =====================================================

        [HttpGet]
        public IActionResult Buscar(string? busqueda)
        {
            var model = new CuentaPorCobrarViewModel
            {
                Busqueda = busqueda?.Trim(),
                EstadoFiltro = "TODOS"
            };

            try
            {
                using var connection =
                    _dbConnection.CreateConnection();

                var fila = connection.QuerySingle(
                    @"CALL public.sp_buscar_cuentas_por_cobrar(
                        @p_busqueda,
                        NULL::TEXT
                    )",
                    new
                    {
                        p_busqueda = model.Busqueda
                    },
                    commandType: CommandType.Text
                );

                string? json = Convert.ToString(fila.o_cuentas);

                ViewBag.Cuentas = DeserializarLista<
                    CuentaPorCobrarViewModel>(json);

                return View("Index", model);
            }
            catch (PostgresException)
            {
                ViewBag.ErrorCuentas =
                    "No fue posible buscar las cuentas del cliente.";

                ViewBag.Cuentas =
                    new List<CuentaPorCobrarViewModel>();

                return View("Index", model);
            }
            catch (Exception)
            {
                ViewBag.ErrorCuentas =
                    "Ocurrió un error al buscar las cuentas.";

                ViewBag.Cuentas =
                    new List<CuentaPorCobrarViewModel>();

                return View("Index", model);
            }
        }

        // =====================================================
        // HU-41: LISTAR CUENTAS VENCIDAS
        // =====================================================

        [HttpGet]
        public IActionResult Vencidas(string? busqueda)
        {
            var model = new CuentaPorCobrarViewModel
            {
                Busqueda = busqueda?.Trim(),
                EstadoFiltro = "VENCIDA"
            };

            try
            {
                using var connection =
                    _dbConnection.CreateConnection();

                var fila = connection.QuerySingle(
                    @"CALL public.sp_listar_cuentas_vencidas(
                        @p_busqueda,
                        NULL::TEXT
                    )",
                    new
                    {
                        p_busqueda = model.Busqueda
                    },
                    commandType: CommandType.Text
                );

                string? json = Convert.ToString(fila.o_cuentas);

                ViewBag.Cuentas = DeserializarLista<
                    CuentaPorCobrarViewModel>(json);

                return View("Index", model);
            }
            catch (PostgresException)
            {
                ViewBag.ErrorCuentas =
                    "No fue posible consultar las cuentas vencidas.";

                ViewBag.Cuentas =
                    new List<CuentaPorCobrarViewModel>();

                return View("Index", model);
            }
            catch (Exception)
            {
                ViewBag.ErrorCuentas =
                    "Ocurrió un error al consultar las cuentas vencidas.";

                ViewBag.Cuentas =
                    new List<CuentaPorCobrarViewModel>();

                return View("Index", model);
            }
        }

        // =====================================================
        // HU-40: CONSULTAR DETALLE DE UNA CUENTA
        // =====================================================

        [HttpGet]
        public IActionResult Detalle(int id)
        {
            if (id <= 0)
            {
                return BadRequest(
                    "El identificador de la cuenta no es válido."
                );
            }

            try
            {
                using var connection =
                    _dbConnection.CreateConnection();

                var fila = connection.QuerySingle(
                    @"CALL public.sp_obtener_detalle_cuenta(
                        @p_id_cxc,
                        NULL::TEXT
                    )",
                    new
                    {
                        p_id_cxc = id
                    },
                    commandType: CommandType.Text
                );

                string? json = Convert.ToString(fila.o_detalle);

                if (string.IsNullOrWhiteSpace(json) ||
                    json == "null")
                {
                    return NotFound(
                        $"La cuenta por cobrar #{id} no existe."
                    );
                }

                var detalle =
                    JsonSerializer.Deserialize<
                        DetalleCuentaPorCobrarViewModel>(
                            json,
                            _jsonOptions
                        );

                if (detalle == null)
                {
                    return NotFound(
                        "No se encontró información de la cuenta."
                    );
                }

                return View(detalle);
            }
            catch (PostgresException)
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "Ocurrió un error al consultar el detalle de la cuenta."
                );
            }
            catch (Exception)
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "No fue posible cargar el detalle de la cuenta."
                );
            }
        }

        // =====================================================
        // HU-37: REGISTRAR PAGO Y ACTUALIZAR SALDO
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistrarPago(
            RegistrarPagoCuentaViewModel model)
        {
            if (model.IdCxc <= 0)
            {
                TempData["ErrorPago"] =
                    "El identificador de la cuenta no es válido.";

                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorPago"] =
                    "Verifique los datos del pago antes de continuar.";

                return RedirectToAction(
                    nameof(Detalle),
                    new { id = model.IdCxc }
                );
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

                var parametros = new DynamicParameters();

                parametros.Add(
                    "p_id_cxc",
                    model.IdCxc,
                    DbType.Int32
                );

                parametros.Add(
                    "p_monto",
                    model.Monto,
                    DbType.Decimal
                );

                parametros.Add(
                    "p_metodo_pago",
                    model.MetodoPago.Trim(),
                    DbType.String
                );

                parametros.Add(
                    "p_referencia",
                    model.Referencia?.Trim(),
                    DbType.String
                );

                parametros.Add(
                    "p_observaciones",
                    model.Observaciones?.Trim(),
                    DbType.String
                );

                parametros.Add(
                    "p_usuario_id",
                    idUsuario,
                    DbType.Int32
                );

                var fila = connection.QuerySingle(
                    @"CALL public.sp_registrar_pago_cuenta(
                        @p_id_cxc,
                        @p_monto,
                        @p_metodo_pago,
                        @p_referencia,
                        @p_observaciones,
                        @p_usuario_id,
                        NULL::VARCHAR,
                        NULL::NUMERIC
                    )",
                    parametros,
                    commandType: CommandType.Text
                );

                string resultado =
                    Convert.ToString(fila.o_resultado)
                    ?? string.Empty;

                decimal? saldoActualizado =
                    fila.o_saldo_actualizado == null
                        ? null
                        : Convert.ToDecimal(
                            fila.o_saldo_actualizado
                        );

                if (resultado == "OK")
                {
                    TempData["ExitoPago"] =
                        $"El pago se registró correctamente. " +
                        $"Saldo pendiente: {saldoActualizado:N2}.";

                    // La notificación es independiente del registro del pago:
                    // un fallo de correo nunca debe revertir ni invalidar el pago.
                    try
                    {
                        var filaDetalle = connection.QuerySingle(
                            @"CALL public.sp_obtener_detalle_cuenta(
                                @p_id_cxc,
                                NULL::TEXT
                            )",
                            new
                            {
                                p_id_cxc = model.IdCxc
                            },
                            commandType: CommandType.Text
                        );

                        string? jsonDetalle =
                            Convert.ToString(filaDetalle.o_detalle);

                        var detalle = string.IsNullOrWhiteSpace(jsonDetalle) ||
                                      jsonDetalle == "null"
                            ? null
                            : JsonSerializer.Deserialize<
                                DetalleCuentaPorCobrarViewModel>(
                                    jsonDetalle,
                                    _jsonOptions
                                );

                        if (detalle == null ||
                            string.IsNullOrWhiteSpace(detalle.Email))
                        {
                            TempData["AvisoCorreo"] =
                                "El pago quedó registrado, pero no se envió la notificación " +
                                "porque el cliente no tiene un correo electrónico registrado.";
                        }
                        else
                        {
                            string nombreCliente =
                                WebUtility.HtmlEncode(detalle.NombreCliente);

                            string montoPagado = model.Monto.ToString(
                                "C",
                                CultureInfo.GetCultureInfo("es-CR")
                            );

                            string saldoTexto = (saldoActualizado ?? 0m).ToString(
                                "C",
                                CultureInfo.GetCultureInfo("es-CR")
                            );

                            string estado = saldoActualizado <= 0
                                ? "Cuenta pagada"
                                : detalle.Estado == "VENCIDA"
                                    ? "Cuenta vencida"
                                    : "Pago parcial";

                            string cuerpoCorreo = $"""
                                <!DOCTYPE html>
                                <html lang="es">
                                <head>
                                    <meta charset="UTF-8">
                                    <meta name="viewport" content="width=device-width, initial-scale=1.0">
                                </head>
                                <body style="margin:0;padding:24px;background-color:#f5f5f5;font-family:Arial,sans-serif;color:#262626;">
                                    <div style="max-width:600px;margin:0 auto;background:#ffffff;border:1px solid #e5e5e5;border-radius:12px;overflow:hidden;">
                                        <div style="padding:24px;background:#171717;color:#ffffff;">
                                            <h1 style="margin:0;font-size:22px;">Charlie Shop</h1>
                                            <p style="margin:8px 0 0;color:#d4d4d4;">Confirmación de pago</p>
                                        </div>
                                        <div style="padding:28px 24px;">
                                            <p>Hola <strong>{nombreCliente}</strong>,</p>
                                            <p>Te confirmamos que hemos recibido correctamente tu pago.</p>
                                            <div style="margin:22px 0;padding:18px;background:#f5f5f5;border-radius:8px;">
                                                <p style="margin:0 0 12px;color:#737373;font-size:13px;">RESUMEN DEL PAGO</p>
                                                <p style="margin:8px 0;">Monto recibido: <strong>{montoPagado}</strong></p>
                                                <p style="margin:8px 0;">Saldo pendiente: <strong>{saldoTexto}</strong></p>
                                                <p style="margin:8px 0;">Estado: <strong>{estado}</strong></p>
                                                <p style="margin:8px 0;">Cuenta por cobrar: <strong>#{detalle.IdCxc}</strong></p>
                                            </div>
                                            <p>Gracias por tu compra y por confiar en Charlie Shop.</p>
                                            <p style="margin-top:28px;">Saludos,<br><strong>Charlie Shop</strong></p>
                                        </div>
                                        <div style="padding:14px 24px;background:#fafafa;border-top:1px solid #e5e5e5;color:#737373;font-size:12px;">
                                            Este es un correo automático de confirmación de pago.
                                        </div>
                                    </div>
                                </body>
                                </html>
                                """;

                            await _emailService.SendAsync(
                                detalle.Email,
                                $"Charlie Shop - Confirmación de pago #{detalle.IdCxc}",
                                cuerpoCorreo
                            );
                        }
                    }
                    catch (Exception)
                    {
                        TempData["AvisoCorreo"] =
                            "El pago quedó registrado correctamente, pero no fue posible enviar " +
                            "el correo de confirmación.";
                    }

                    return RedirectToAction(
                        nameof(Detalle),
                        new { id = model.IdCxc }
                    );
                }

                TempData["ErrorPago"] = resultado switch
                {
                    "MONTO_INVALIDO" =>
                        "El monto del pago debe ser mayor que cero.",

                    "METODO_PAGO_INVALIDO" =>
                        "Debe seleccionar un método de pago válido.",

                    "USUARIO_INVALIDO" =>
                        "El usuario que registra el pago no es válido o está inactivo.",

                    "CUENTA_NO_ENCONTRADA" =>
                        "La cuenta por cobrar indicada no existe.",

                    "CUENTA_PAGADA" =>
                        "Esta cuenta ya se encuentra completamente pagada.",

                    "MONTO_MAYOR_AL_SALDO" =>
                        $"El monto supera el saldo pendiente de " +
                        $"{saldoActualizado:N2}.",

                    _ =>
                        "No fue posible registrar el pago."
                };

                return RedirectToAction(
                    nameof(Detalle),
                    new { id = model.IdCxc }
                );
            }
            catch (PostgresException)
            {
                TempData["ErrorPago"] =
                    "Ocurrió un error en la base de datos al registrar el pago.";

                return RedirectToAction(
                    nameof(Detalle),
                    new { id = model.IdCxc }
                );
            }
            catch (Exception)
            {
                TempData["ErrorPago"] =
                    "Ocurrió un error inesperado al registrar el pago.";

                return RedirectToAction(
                    nameof(Detalle),
                    new { id = model.IdCxc }
                );
            }
        }

        // =====================================================
        // HU-42: CONSULTAR HISTORIAL DE PAGOS
        // Filtros opcionales por cliente y cuenta.
        // =====================================================

        [HttpGet]
        public IActionResult HistorialPagos(
            int? idCliente,
            int? idCxc)
        {
            if (idCliente.HasValue && idCliente.Value <= 0)
            {
                return BadRequest(
                    "El identificador del cliente no es válido."
                );
            }

            if (idCxc.HasValue && idCxc.Value <= 0)
            {
                return BadRequest(
                    "El identificador de la cuenta no es válido."
                );
            }

            try
            {
                using var connection =
                    _dbConnection.CreateConnection();

                var fila = connection.QuerySingle(
                    @"CALL public.sp_consultar_historial_pagos(
                        CAST(@p_id_cliente AS INTEGER),
                        CAST(@p_id_cxc AS INTEGER),
                        NULL::TEXT
                    )",
                    new
                    {
                        p_id_cliente = idCliente,
                        p_id_cxc = idCxc
                    },
                    commandType: CommandType.Text
                );

                string? json = Convert.ToString(fila.o_pagos);

                var pagos = DeserializarLista<
                    HistorialPagoCuentaViewModel>(json);

                ViewBag.IdCliente = idCliente;
                ViewBag.IdCxc = idCxc;

                return View(pagos);
            }
            catch (PostgresException)
            {
                ViewBag.ErrorHistorial =
                    "No fue posible consultar el historial de pagos.";

                return View(
                    new List<HistorialPagoCuentaViewModel>()
                );
            }
            catch (Exception)
            {
                ViewBag.ErrorHistorial =
                    "Ocurrió un error al cargar el historial de pagos.";

                return View(
                    new List<HistorialPagoCuentaViewModel>()
                );
            }
        }

        // =====================================================
        // MÉTODO AUXILIAR
        // DESERIALIZAR RESULTADOS JSON
        // =====================================================

        private List<T> DeserializarLista<T>(string? json)
        {
            if (string.IsNullOrWhiteSpace(json) ||
                json == "null")
            {
                return new List<T>();
            }

            return JsonSerializer.Deserialize<List<T>>(
                json,
                _jsonOptions
            ) ?? new List<T>();
        }
    }
}
