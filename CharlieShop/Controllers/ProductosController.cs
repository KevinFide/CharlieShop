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

        // =========================================================
        // HU-07
        // Mostrar formulario para registrar y crear producto
        // =========================================================
        [HttpGet]
        public IActionResult Crear()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Crear(CrearProductoViewModel modelo)
        {
            if (!ModelState.IsValid)
            {
                return View(modelo);
            }

            using var connection =
                (NpgsqlConnection)_dbConnection.CreateConnection();

            connection.Open();

            using var command = new NpgsqlCommand(
                "CALL sp_crear_producto(@p_codigo, @p_nombre, @p_descripcion, @p_categoria, @p_precio_venta, @p_costo, @p_unidad_medida, NULL);",
                connection);

            command.Parameters.AddWithValue("p_codigo", modelo.Codigo);
            command.Parameters.AddWithValue("p_nombre", modelo.Nombre);
            command.Parameters.AddWithValue(
                "p_descripcion",
                (object?)modelo.Descripcion ?? DBNull.Value);
            command.Parameters.AddWithValue(
                "p_categoria",
                (object?)modelo.Categoria ?? DBNull.Value);
            command.Parameters.AddWithValue(
                "p_precio_venta",
                modelo.PrecioVenta);
            command.Parameters.AddWithValue(
                "p_costo",
                (object?)modelo.Costo ?? DBNull.Value);
            command.Parameters.AddWithValue(
                "p_unidad_medida",
                (object?)modelo.UnidadMedida ?? DBNull.Value);

            var resultado = command.ExecuteScalar()?.ToString();

            switch (resultado)
            {
                case "CREADO":
                    TempData["Success"] = "El producto fue registrado correctamente.";
                    return RedirectToAction(nameof(Index));

                case "CODIGO_EXISTENTE":
                    ModelState.AddModelError(
                        nameof(modelo.Codigo),
                        "El código del producto ya existe.");
                    break;

                default:
                    ModelState.AddModelError(
                        "",
                        "No fue posible registrar el producto.");
                    break;
            }

            return View(modelo);
        }

        // =========================================================
        // HU-08
        // Editar informacion de un producto
        // =========================================================
        [HttpGet]
        public IActionResult Editar(int id)
        {
            using var connection =
                (NpgsqlConnection)_dbConnection.CreateConnection();

            connection.Open();

            using var command = new NpgsqlCommand(
                @"SELECT id_producto, codigo, nombre, descripcion,
                 categoria, precio_venta, costo, unidad_medida
          FROM producto
          WHERE id_producto = @id",
                connection);

            command.Parameters.AddWithValue("id", id);

            using var reader = command.ExecuteReader();

            if (!reader.Read())
            {
                return NotFound();
            }

            var modelo = new EditarProductoViewModel
            {
                IdProducto = reader.GetInt32(0),
                Codigo = reader.GetString(1),
                Nombre = reader.GetString(2),
                Descripcion = reader.IsDBNull(3) ? null : reader.GetString(3),
                Categoria = reader.IsDBNull(4) ? null : reader.GetString(4),
                PrecioVenta = reader.GetDecimal(5),
                Costo = reader.IsDBNull(6) ? null : reader.GetDecimal(6),
                UnidadMedida = reader.IsDBNull(7) ? null : reader.GetString(7)
            };

            return View(modelo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Editar(EditarProductoViewModel modelo)
        {
            if (!ModelState.IsValid)
            {
                return View(modelo);
            }

            using var connection =
                (NpgsqlConnection)_dbConnection.CreateConnection();

            connection.Open();

            using var command = new NpgsqlCommand(
                "CALL sp_editar_producto(@p_id_producto, @p_codigo, @p_nombre, @p_descripcion, @p_categoria, @p_precio_venta, @p_costo, @p_unidad_medida, NULL);",
                connection);

            command.Parameters.AddWithValue(
                "p_id_producto",
                modelo.IdProducto);

            command.Parameters.AddWithValue(
                "p_codigo",
                modelo.Codigo);

            command.Parameters.AddWithValue(
                "p_nombre",
                modelo.Nombre);

            command.Parameters.AddWithValue(
                "p_descripcion",
                (object?)modelo.Descripcion ?? DBNull.Value);

            command.Parameters.AddWithValue(
                "p_categoria",
                (object?)modelo.Categoria ?? DBNull.Value);

            command.Parameters.AddWithValue(
                "p_precio_venta",
                modelo.PrecioVenta);

            command.Parameters.AddWithValue(
                "p_costo",
                (object?)modelo.Costo ?? DBNull.Value);

            command.Parameters.AddWithValue(
                "p_unidad_medida",
                (object?)modelo.UnidadMedida ?? DBNull.Value);

            var resultado = command.ExecuteScalar()?.ToString();

            switch (resultado)
            {
                case "ACTUALIZADO":
                    TempData["Success"] =
                        "El producto fue actualizado correctamente.";

                    return RedirectToAction(nameof(Index));

                case "CODIGO_EXISTENTE":
                    ModelState.AddModelError(
                        nameof(modelo.Codigo),
                        "El código del producto ya existe.");
                    break;

                case "NO_ENCONTRADO":
                    return NotFound();

                default:
                    ModelState.AddModelError(
                        "",
                        "No fue posible actualizar el producto.");
                    break;
            }

            return View(modelo);
        }

        // =========================================================
        // HU-09
        // Eliminar producto
        // =========================================================
        [HttpGet]
        public IActionResult Eliminar(int id)
        {
            using var connection =
                (NpgsqlConnection)_dbConnection.CreateConnection();

            connection.Open();

            using var command = new NpgsqlCommand(
                @"SELECT id_producto, codigo, nombre
          FROM producto
          WHERE id_producto = @id;",
                connection);

            command.Parameters.AddWithValue("id", id);

            using var reader = command.ExecuteReader();

            if (!reader.Read())
            {
                TempData["Error"] = "El producto solicitado no fue encontrado.";
                return RedirectToAction(nameof(Index));
            }

            var producto = new Producto
            {
                IdProducto = reader.GetInt32(0),
                Codigo = reader.GetString(1),
                Nombre = reader.GetString(2)
            };

            return View(producto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EliminarConfirmado(int id)
        {
            Console.WriteLine($"ID RECIBIDO: {id}");

            using var connection =
                (NpgsqlConnection)_dbConnection.CreateConnection();

            connection.Open();

            using var command = new NpgsqlCommand(
                "CALL sp_eliminar_producto(@p_id_producto, NULL);",
                connection);

            command.Parameters.AddWithValue(
                "p_id_producto",
                id);

            var resultado = command.ExecuteScalar()?.ToString();

            switch (resultado)
            {
                case "ELIMINADO":
                    TempData["Success"] =
                        "El producto fue eliminado correctamente.";
                    break;

                case "NO_ENCONTRADO":
                    TempData["Error"] =
                        "El producto solicitado no fue encontrado.";
                    break;

                case "TIENE_VENTAS":
                    TempData["Error"] =
                        "No se puede eliminar el producto porque tiene ventas asociadas.";
                    break;

                case "TIENE_MOVIMIENTOS":
                    TempData["Error"] =
                        "No se puede eliminar el producto porque tiene movimientos de inventario.";
                    break;

                default:
                    TempData["Error"] =
                        "No fue posible eliminar el producto.";
                    break;
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================================================
        // HU-11 / HU-13
        // Listado, búsqueda y filtro por categoría
        // =========================================================
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

        // =========================================================
        // HU-12
        // Mostrar formulario para actualizar precio
        // =========================================================
        [HttpGet]
        public IActionResult EditarPrecio(int id)
        {
            using var connection =
                (NpgsqlConnection)_dbConnection.CreateConnection();

            connection.Open();

            using var command = new NpgsqlCommand(
                @"SELECT
                    id_producto,
                    codigo,
                    nombre,
                    precio_venta
                  FROM producto
                  WHERE id_producto = @id;",
                connection);

            command.Parameters.AddWithValue("id", id);

            using var reader = command.ExecuteReader();

            if (!reader.Read())
            {
                return NotFound();
            }

            var modelo = new EditarPrecioProductoViewModel
            {
                IdProducto = reader.GetInt32(0),
                Codigo = reader.GetString(1),
                Nombre = reader.GetString(2),
                PrecioActual = reader.GetDecimal(3),
                NuevoPrecio = reader.GetDecimal(3)
            };

            return View(modelo);
        }

        // =========================================================
        // HU-12
        // Guardar nuevo precio
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditarPrecio(
            EditarPrecioProductoViewModel modelo)
        {
            if (modelo.NuevoPrecio <= 0)
            {
                ModelState.AddModelError(
                    nameof(modelo.NuevoPrecio),
                    "El precio debe ser mayor que cero.");
            }

            if (!ModelState.IsValid)
            {
                return View(modelo);
            }

            using var connection =
                (NpgsqlConnection)_dbConnection.CreateConnection();

            connection.Open();

            using var command = new NpgsqlCommand(
                "CALL sp_actualizar_precio_producto(@p_id_producto, @p_precio_venta, NULL);",
                connection);

            command.Parameters.AddWithValue(
                "p_id_producto",
                modelo.IdProducto);

            command.Parameters.AddWithValue(
                "p_precio_venta",
                modelo.NuevoPrecio);

            var resultado = command.ExecuteScalar()?.ToString();

            switch (resultado)
            {
                case "ACTUALIZADO":
                    TempData["Success"] =
                        "El precio del producto se actualizó correctamente.";

                    return RedirectToAction(nameof(Index));

                case "PRECIO_INVALIDO":
                    ModelState.AddModelError(
                        nameof(modelo.NuevoPrecio),
                        "Ingrese un precio válido mayor que cero.");
                    break;

                case "PRODUCTO_NO_ENCONTRADO":
                    return NotFound();
            }

            return View(modelo);
        }

        // =========================================================
        // HU-14
        // Consultar detalle de un producto
        // =========================================================
        public IActionResult Detalle(int id)
        {
            using var connection =
                (NpgsqlConnection)_dbConnection.CreateConnection();

            connection.Open();

            using var command = new NpgsqlCommand(
                "CALL sp_obtener_producto(@p_id_producto, NULL);",
                connection);

            command.Parameters.AddWithValue(
                "p_id_producto",
                id);

            var resultado = command.ExecuteScalar()?.ToString();

            if (string.IsNullOrWhiteSpace(resultado) ||
                resultado == "{}" ||
                resultado == "null")
            {
                TempData["Error"] =
                    "El producto solicitado no fue encontrado.";

                return RedirectToAction(nameof(Index));
            }

            var producto = JsonSerializer.Deserialize<Producto>(
                resultado,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (producto == null)
            {
                TempData["Error"] =
                    "El producto solicitado no fue encontrado.";

                return RedirectToAction(nameof(Index));
            }

            return View(producto);
        }

        // =========================================================
        // HU-15
        // Activar / desactivar producto
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CambiarEstado(int id)
        {
            using var connection =
                (NpgsqlConnection)_dbConnection.CreateConnection();

            connection.Open();

            // -----------------------------------------------------
            // Consultar estado actual
            // -----------------------------------------------------
            using var consulta = new NpgsqlCommand(
                @"SELECT estado
                  FROM producto
                  WHERE id_producto = @id;",
                connection);

            consulta.Parameters.AddWithValue("id", id);

            var estadoActual = consulta.ExecuteScalar();

            if (estadoActual == null)
            {
                TempData["Error"] =
                    "El producto solicitado no fue encontrado.";

                return RedirectToAction(nameof(Index));
            }

            bool estado = Convert.ToBoolean(estadoActual);

            // Invertimos el estado actual.
            bool nuevoEstado = !estado;

            // -----------------------------------------------------
            // Cambiar estado mediante procedimiento almacenado
            // -----------------------------------------------------
            using var command = new NpgsqlCommand(
                "CALL sp_cambiar_estado_producto(@p_id_producto, @p_estado, NULL);",
                connection);

            command.Parameters.AddWithValue(
                "p_id_producto",
                id);

            command.Parameters.AddWithValue(
                "p_estado",
                nuevoEstado);

            var resultado = command.ExecuteScalar()?.ToString();

            switch (resultado)
            {
                case "ACTIVADO":

                    TempData["Success"] =
                        "El producto fue activado correctamente.";

                    break;

                case "DESACTIVADO":

                    TempData["Success"] =
                        "El producto fue desactivado correctamente.";

                    break;

                case "PRODUCTO_NO_ENCONTRADO":

                    TempData["Error"] =
                        "El producto solicitado no fue encontrado.";

                    break;

                default:

                    TempData["Error"] =
                        "No fue posible cambiar el estado del producto.";

                    break;
            }

            return RedirectToAction(nameof(Index));
        }
    }
}