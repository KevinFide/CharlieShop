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
    public class DashboardController : Controller
    {
        private readonly DbConnection _dbConnection;

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public DashboardController(DbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        [HttpGet]
        public IActionResult Index()
        {
            try
            {
                using var connection = _dbConnection.CreateConnection();

                // Toda la consulta del dashboard se centraliza en el procedimiento almacenado.
                var resultado = connection.QuerySingle<string>(
                    "CALL sp_obtener_dashboard(NULL::JSON)",
                    commandType: CommandType.Text
                );

                var model = string.IsNullOrWhiteSpace(resultado)
                    ? new DashboardViewModel()
                    : JsonSerializer.Deserialize<DashboardViewModel>(
                        resultado,
                        _jsonOptions
                    ) ?? new DashboardViewModel();

                return View(model);
            }
            catch (PostgresException)
            {
                TempData["Error"] =
                    "No fue posible cargar el dashboard. Verifique que el procedimiento almacenado sp_obtener_dashboard esté creado y actualizado.";

                return View(new DashboardViewModel());
            }
            catch (NpgsqlException)
            {
                TempData["Error"] =
                    "No fue posible conectar con la base de datos.";

                return View(new DashboardViewModel());
            }
            catch (Exception)
            {
                TempData["Error"] =
                    "Ocurrió un error inesperado al cargar el dashboard.";

                return View(new DashboardViewModel());
            }
        }
    }
}