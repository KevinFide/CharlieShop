using CharlieShop.Data;
using CharlieShop.Models;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Text.Json;

namespace CharlieShop.Controllers
{
    public class ProductosController : Controller
    {
        private readonly DbConnection _dbConnection;

        public ProductosController(DbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public IActionResult Index(
            string? busqueda,
            string? categoria)
        {
            var productos = new List<Producto>();

            using var connection =
                (NpgsqlConnection)_dbConnection.CreateConnection();

            connection.Open();

            using var command = new NpgsqlCommand(
                "CALL sp_obtener_productos(@p_busqueda, @p_categoria, NULL);",
                connection);

            command.Parameters.AddWithValue(
                "p_busqueda",
                (object?)busqueda ?? DBNull.Value);

            command.Parameters.AddWithValue(
                "p_categoria",
                (object?)categoria ?? DBNull.Value);

            var resultado = command.ExecuteScalar()?.ToString();

            if (!string.IsNullOrWhiteSpace(resultado))
            {
                productos = JsonSerializer.Deserialize<List<Producto>>(
                    resultado,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new List<Producto>();
            }

            ViewBag.Busqueda = busqueda;
            ViewBag.Categoria = categoria;

            return View(productos);
        }

        public IActionResult Detalle()
        {
            return View();
        }
    }
}