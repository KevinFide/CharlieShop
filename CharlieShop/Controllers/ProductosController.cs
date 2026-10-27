using CharlieShop.Data;
using CharlieShop.Models;
using Dapper;
using System.Data;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;

namespace CharlieShop.Controllers
{
    public class ProductosController : Controller
    {
        private readonly DbConnection _dbConnection;
        private readonly IWebHostEnvironment _environment;

        private const long TamanoMaximoImagen = 5 * 1024 * 1024;
        private static readonly HashSet<string> ExtensionesPermitidas =
            new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

        public ProductosController(
            DbConnection dbConnection,
            IWebHostEnvironment environment)
        {
            _dbConnection = dbConnection;
            _environment = environment;
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
        public async Task<IActionResult> Crear(CrearProductoViewModel modelo)
        {
            string? rutaFisicaImagen = null;
            string? rutaImagenRelativa = null;

            if (modelo.ImagenArchivo is { Length: > 0 } archivo)
            {
                var extension = Path.GetExtension(archivo.FileName);
                var tiposMimePermitidos = new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    "image/jpeg", "image/png", "image/webp"
                };

                if (!ExtensionesPermitidas.Contains(extension))
                {
                    ModelState.AddModelError(
                        nameof(modelo.ImagenArchivo),
                        "La imagen debe ser JPG, JPEG, PNG o WebP.");
                }

                if (archivo.Length > TamanoMaximoImagen)
                {
                    ModelState.AddModelError(
                        nameof(modelo.ImagenArchivo),
                        "La imagen no puede superar los 5 MB.");
                }

                if (!tiposMimePermitidos.Contains(archivo.ContentType))
                {
                    ModelState.AddModelError(
                        nameof(modelo.ImagenArchivo),
                        "El archivo seleccionado no tiene un tipo de imagen permitido.");
                }

                if (ModelState.IsValid)
                {
                    await using var input = archivo.OpenReadStream();
                    var firma = new byte[12];
                    var bytesLeidos = await input.ReadAsync(firma, 0, firma.Length);

                    bool esJpeg = bytesLeidos >= 3 &&
                                  firma[0] == 0xFF && firma[1] == 0xD8 && firma[2] == 0xFF;
                    bool esPng = bytesLeidos >= 8 &&
                                 firma[0] == 0x89 && firma[1] == 0x50 &&
                                 firma[2] == 0x4E && firma[3] == 0x47 &&
                                 firma[4] == 0x0D && firma[5] == 0x0A &&
                                 firma[6] == 0x1A && firma[7] == 0x0A;
                    bool esWebp = bytesLeidos >= 12 &&
                                  firma[0] == (byte)'R' && firma[1] == (byte)'I' &&
                                  firma[2] == (byte)'F' && firma[3] == (byte)'F' &&
                                  firma[8] == (byte)'W' && firma[9] == (byte)'E' &&
                                  firma[10] == (byte)'B' && firma[11] == (byte)'P';

                    bool extensionYContenidoCoinciden =
                        (esJpeg &&
                         (extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                          extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)) &&
                         archivo.ContentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase))
                        || (esPng &&
                            extension.Equals(".png", StringComparison.OrdinalIgnoreCase) &&
                            archivo.ContentType.Equals("image/png", StringComparison.OrdinalIgnoreCase))
                        || (esWebp &&
                            extension.Equals(".webp", StringComparison.OrdinalIgnoreCase) &&
                            archivo.ContentType.Equals("image/webp", StringComparison.OrdinalIgnoreCase));

                    if (!extensionYContenidoCoinciden)
                    {
                        ModelState.AddModelError(
                            nameof(modelo.ImagenArchivo),
                            "El contenido del archivo no coincide con una imagen JPG, PNG o WebP válida.");
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                return View(modelo);
            }

            try
            {
                // La imagen es opcional: solo se guarda si se seleccionó una.
                if (modelo.ImagenArchivo is { Length: > 0 } imagen)
                {
                    var extension = Path.GetExtension(imagen.FileName).ToLowerInvariant();
                    var nombreArchivo = $"{Guid.NewGuid():N}{extension}";
                    var raizWeb = _environment.WebRootPath
                        ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
                    var carpetaImagenes = Path.Combine(
                        raizWeb, "uploads", "productos");

                    Directory.CreateDirectory(carpetaImagenes);

                    rutaFisicaImagen = Path.Combine(carpetaImagenes, nombreArchivo);
                    rutaImagenRelativa = $"/uploads/productos/{nombreArchivo}";

                    await using var fileStream = new FileStream(
                        rutaFisicaImagen,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None);

                    await imagen.CopyToAsync(fileStream);
                }

                using var connection =
                    (NpgsqlConnection)_dbConnection.CreateConnection();

                connection.Open();

                using var command = new NpgsqlCommand(
                    "CALL sp_crear_producto(@p_codigo, @p_nombre, @p_descripcion, @p_categoria, @p_precio_venta, @p_costo, @p_unidad_medida, @p_imagen_url, NULL);",
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
                command.Parameters.Add(new NpgsqlParameter(
                    "p_imagen_url",
                    NpgsqlTypes.NpgsqlDbType.Varchar)
                {
                    Value = (object?)rutaImagenRelativa ?? DBNull.Value
                });

                var resultado = command.ExecuteScalar()?.ToString();

                switch (resultado)
                {
                    case "CREADO":
                        TempData["Success"] =
                            "El producto fue registrado correctamente.";
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
            }
            catch (Exception)
            {
                ModelState.AddModelError(
                    "",
                    "Ocurrió un error al registrar el producto. Verificá los datos e intentá nuevamente.");
            }

            // Si la base de datos rechaza el registro, eliminar el archivo temporal.
            if (!string.IsNullOrWhiteSpace(rutaFisicaImagen) &&
                System.IO.File.Exists(rutaFisicaImagen))
            {
                try
                {
                    System.IO.File.Delete(rutaFisicaImagen);
                }
                catch
                {
                    // La limpieza no debe ocultar el error del registro.
                }
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
                         categoria, precio_venta, costo, unidad_medida, imagen_url
                  FROM producto
                  WHERE id_producto = @id;",
                connection);

            command.Parameters.AddWithValue("id", id);

            using var reader = command.ExecuteReader();

            if (!reader.Read())
                return NotFound();

            var modelo = new EditarProductoViewModel
            {
                IdProducto = reader.GetInt32(0),
                Codigo = reader.GetString(1),
                Nombre = reader.GetString(2),
                Descripcion = reader.IsDBNull(3) ? null : reader.GetString(3),
                Categoria = reader.IsDBNull(4) ? null : reader.GetString(4),
                PrecioVenta = reader.GetDecimal(5),
                Costo = reader.IsDBNull(6) ? null : reader.GetDecimal(6),
                UnidadMedida = reader.IsDBNull(7) ? null : reader.GetString(7),
                ImagenUrl = reader.IsDBNull(8) ? null : reader.GetString(8)
            };

            return View(modelo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(EditarProductoViewModel modelo)
        {
            string? nuevaRutaFisica = null;
            string? nuevaRutaRelativa = null;
            string? imagenAnterior = null;

            // Validar la imagen antes de guardarla.
            if (modelo.ImagenArchivo is { Length: > 0 } archivo)
            {
                var extension = Path.GetExtension(archivo.FileName);
                var tiposMimePermitidos = new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    "image/jpeg", "image/png", "image/webp"
                };

                if (!ExtensionesPermitidas.Contains(extension))
                    ModelState.AddModelError(nameof(modelo.ImagenArchivo),
                        "La imagen debe ser JPG, JPEG, PNG o WebP.");

                if (archivo.Length > TamanoMaximoImagen)
                    ModelState.AddModelError(nameof(modelo.ImagenArchivo),
                        "La imagen no puede superar los 5 MB.");

                if (!tiposMimePermitidos.Contains(archivo.ContentType))
                    ModelState.AddModelError(nameof(modelo.ImagenArchivo),
                        "El archivo seleccionado no tiene un tipo de imagen permitido.");

                if (ModelState.IsValid)
                {
                    await using var input = archivo.OpenReadStream();
                    var firma = new byte[12];
                    var bytesLeidos = await input.ReadAsync(firma, 0, firma.Length);

                    bool esJpeg = bytesLeidos >= 3 &&
                        firma[0] == 0xFF && firma[1] == 0xD8 && firma[2] == 0xFF;
                    bool esPng = bytesLeidos >= 8 &&
                        firma[0] == 0x89 && firma[1] == 0x50 &&
                        firma[2] == 0x4E && firma[3] == 0x47 &&
                        firma[4] == 0x0D && firma[5] == 0x0A &&
                        firma[6] == 0x1A && firma[7] == 0x0A;
                    bool esWebp = bytesLeidos >= 12 &&
                        firma[0] == (byte)'R' && firma[1] == (byte)'I' &&
                        firma[2] == (byte)'F' && firma[3] == (byte)'F' &&
                        firma[8] == (byte)'W' && firma[9] == (byte)'E' &&
                        firma[10] == (byte)'B' && firma[11] == (byte)'P';

                    bool coincide =
                        (esJpeg &&
                         (extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                          extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)) &&
                         archivo.ContentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase))
                        || (esPng &&
                            extension.Equals(".png", StringComparison.OrdinalIgnoreCase) &&
                            archivo.ContentType.Equals("image/png", StringComparison.OrdinalIgnoreCase))
                        || (esWebp &&
                            extension.Equals(".webp", StringComparison.OrdinalIgnoreCase) &&
                            archivo.ContentType.Equals("image/webp", StringComparison.OrdinalIgnoreCase));

                    if (!coincide)
                        ModelState.AddModelError(nameof(modelo.ImagenArchivo),
                            "El contenido del archivo no coincide con una imagen JPG, PNG o WebP válida.");
                }
            }

            if (!ModelState.IsValid)
            {
                // Recuperar la imagen actual para que la vista pueda mostrarla.
                using var db = (NpgsqlConnection)_dbConnection.CreateConnection();
                db.Open();
                using var consultaImagen = new NpgsqlCommand(
                    "SELECT imagen_url FROM producto WHERE id_producto = @id;", db);
                consultaImagen.Parameters.AddWithValue("id", modelo.IdProducto);
                imagenAnterior = consultaImagen.ExecuteScalar() as string;
                modelo.ImagenUrl = imagenAnterior;
                return View(modelo);
            }

            try
            {
                using var connection =
                    (NpgsqlConnection)_dbConnection.CreateConnection();
                connection.Open();

                // Leer la ruta actual desde la base de datos; no confiar en el valor oculto del formulario.
                using (var consultaImagen = new NpgsqlCommand(
                    "SELECT imagen_url FROM producto WHERE id_producto = @id;", connection))
                {
                    consultaImagen.Parameters.AddWithValue("id", modelo.IdProducto);
                    imagenAnterior = consultaImagen.ExecuteScalar() as string;
                }

                // Guardar la imagen nueva, si el usuario seleccionó una.
                if (modelo.ImagenArchivo is { Length: > 0 } imagen)
                {
                    var extension = Path.GetExtension(imagen.FileName).ToLowerInvariant();
                    var nombreArchivo = $"{Guid.NewGuid():N}{extension}";
                    var raizWeb = _environment.WebRootPath
                        ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
                    var carpetaImagenes = Path.Combine(raizWeb, "uploads", "productos");
                    Directory.CreateDirectory(carpetaImagenes);

                    nuevaRutaFisica = Path.Combine(carpetaImagenes, nombreArchivo);
                    nuevaRutaRelativa = $"/uploads/productos/{nombreArchivo}";

                    await using var fileStream = new FileStream(
                        nuevaRutaFisica, FileMode.CreateNew,
                        FileAccess.Write, FileShare.None);
                    await imagen.CopyToAsync(fileStream);
                }

                using var command = new NpgsqlCommand(
                    "CALL sp_editar_producto(@p_id_producto, @p_codigo, @p_nombre, @p_descripcion, @p_categoria, @p_precio_venta, @p_costo, @p_unidad_medida, NULL);",
                    connection);

                command.Parameters.AddWithValue("p_id_producto", modelo.IdProducto);
                command.Parameters.AddWithValue("p_codigo", modelo.Codigo);
                command.Parameters.AddWithValue("p_nombre", modelo.Nombre);
                command.Parameters.AddWithValue("p_descripcion",
                    (object?)modelo.Descripcion ?? DBNull.Value);
                command.Parameters.AddWithValue("p_categoria",
                    (object?)modelo.Categoria ?? DBNull.Value);
                command.Parameters.AddWithValue("p_precio_venta", modelo.PrecioVenta);
                command.Parameters.AddWithValue("p_costo",
                    (object?)modelo.Costo ?? DBNull.Value);
                command.Parameters.AddWithValue("p_unidad_medida",
                    (object?)modelo.UnidadMedida ?? DBNull.Value);

                var resultado = command.ExecuteScalar()?.ToString();

                if (resultado == "ACTUALIZADO")
                {
                    // Solo cambiar imagen_url cuando se subió una imagen nueva.
                    if (nuevaRutaRelativa != null)
                    {
                        using var actualizarImagen = new NpgsqlCommand(
                            "UPDATE producto SET imagen_url = @imagen_url WHERE id_producto = @id;",
                            connection);
                        actualizarImagen.Parameters.AddWithValue("imagen_url", nuevaRutaRelativa);
                        actualizarImagen.Parameters.AddWithValue("id", modelo.IdProducto);
                        actualizarImagen.ExecuteNonQuery();
                    }

                    // Eliminar la imagen anterior únicamente después de guardar los cambios.
                    if (nuevaRutaRelativa != null)
                        EliminarImagenProductoAnterior(imagenAnterior);

                    TempData["Success"] = "El producto fue actualizado correctamente.";
                    return RedirectToAction(nameof(Index));
                }

                if (nuevaRutaFisica != null && System.IO.File.Exists(nuevaRutaFisica))
                    System.IO.File.Delete(nuevaRutaFisica);
                nuevaRutaFisica = null;

                switch (resultado)
                {
                    case "CODIGO_EXISTENTE":
                        ModelState.AddModelError(nameof(modelo.Codigo),
                            "El código del producto ya existe.");
                        break;
                    case "NO_ENCONTRADO":
                        return NotFound();
                    default:
                        ModelState.AddModelError("",
                            "No fue posible actualizar el producto.");
                        break;
                }
            }
            catch
            {
                if (nuevaRutaFisica != null && System.IO.File.Exists(nuevaRutaFisica))
                {
                    try { System.IO.File.Delete(nuevaRutaFisica); } catch { }
                }

                ModelState.AddModelError("",
                    "Ocurrió un error al actualizar el producto. Verificá los datos e intentá nuevamente.");
            }

            modelo.ImagenUrl = imagenAnterior;
            return View(modelo);
        }

        private void EliminarImagenProductoAnterior(string? rutaRelativa)
        {
            if (string.IsNullOrWhiteSpace(rutaRelativa) ||
                !rutaRelativa.StartsWith("/uploads/productos/", StringComparison.OrdinalIgnoreCase))
                return;

            var nombreArchivo = Path.GetFileName(rutaRelativa);
            if (string.IsNullOrWhiteSpace(nombreArchivo))
                return;

            var raizWeb = _environment.WebRootPath
                ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
            var rutaFisica = Path.Combine(raizWeb, "uploads", "productos", nombreArchivo);

            try
            {
                if (System.IO.File.Exists(rutaFisica))
                    System.IO.File.Delete(rutaFisica);
            }
            catch
            {
                // No fallar la actualización si no se logra limpiar el archivo anterior.
            }
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

            // Consultar los cinco movimientos más recientes de este producto.
            var parametrosMovimientos = new DynamicParameters();
            parametrosMovimientos.Add("p_id_producto", id, DbType.Int32);
            parametrosMovimientos.Add("p_tipo_movimiento", "TODOS", DbType.String);
            parametrosMovimientos.Add("p_fecha_desde", dbType: DbType.Date, value: null);
            parametrosMovimientos.Add("p_fecha_hasta", dbType: DbType.Date, value: null);
            parametrosMovimientos.Add("p_pagina", 1, DbType.Int32);
            parametrosMovimientos.Add("p_tamano_pagina", 5, DbType.Int32);
            parametrosMovimientos.Add(
                "o_movimientos",
                dbType: DbType.String,
                direction: ParameterDirection.Output);

            connection.Execute(
                "sp_listar_movimientos_inventario",
                parametrosMovimientos,
                commandType: CommandType.StoredProcedure);

            var movimientosJson = parametrosMovimientos.Get<string?>("o_movimientos");

            var movimientos = string.IsNullOrWhiteSpace(movimientosJson)
                ? new List<MovimientoInventarioViewModel>()
                : JsonSerializer.Deserialize<List<MovimientoInventarioViewModel>>(
                    movimientosJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                  ?? new List<MovimientoInventarioViewModel>();

            var modeloDetalle = new ProductoDetalleViewModel
            {
                Producto = producto,
                UltimosMovimientos = movimientos
            };

            return View(modeloDetalle);
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