
using CharlieShop.Data;
using CharlieShop.Models;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using NpgsqlTypes;
using System.Data;

namespace CharlieShop.Controllers
{
    public class ClientesController : Controller
    {
        private readonly DbConnection _dbConnection;

        public ClientesController(DbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        // HU-31: Consultar listado de clientes
        [HttpGet]
        public IActionResult Index()
        {
            var clientes = ObtenerClientes();
            return View(clientes);
        }

        // HU-32: Buscar clientes
        [HttpGet]
        public IActionResult Buscar(string? termino)
        {
            var clientes = ObtenerClientes();

            if (!string.IsNullOrWhiteSpace(termino))
            {
                termino = termino.Trim();

                clientes = clientes
                    .Where(c =>
                        c.Nombre.Contains(
                            termino, StringComparison.OrdinalIgnoreCase) ||

                        c.Apellido.Contains(
                            termino, StringComparison.OrdinalIgnoreCase) ||

                        (c.Identificacion?.Contains(
                            termino, StringComparison.OrdinalIgnoreCase) ?? false) ||

                        (c.Telefono?.Contains(
                            termino, StringComparison.OrdinalIgnoreCase) ?? false))
                    .ToList();
            }

            ViewBag.Termino = termino;
            return View("Index", clientes);
        }

        // HU-33: Consultar detalle de un cliente

        [HttpGet]
        public IActionResult Detalle(int id)
        {
            ClienteDetalleViewModel? cliente = null;

            using var connection =
                (NpgsqlConnection)_dbConnection.CreateConnection();

            connection.Open();

            using var command = new NpgsqlCommand(
                @"SELECT
              id_cliente,
              nombre,
              apellido,
              identificacion,
              telefono,
              email,
              direccion,
              fecha_registro,
              estado
          FROM public.cliente
          WHERE id_cliente = @id;",
                connection);

            command.Parameters.AddWithValue("id", id);

            using var reader = command.ExecuteReader();

            if (reader.Read())
            {
                cliente = new ClienteDetalleViewModel
                {
                    IdCliente = reader.GetInt32(reader.GetOrdinal("id_cliente")),
                    Nombre = reader.GetString(reader.GetOrdinal("nombre")),
                    Apellido = reader.GetString(reader.GetOrdinal("apellido")),

                    Identificacion = reader.IsDBNull(reader.GetOrdinal("identificacion"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("identificacion")),

                    Telefono = reader.IsDBNull(reader.GetOrdinal("telefono"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("telefono")),

                    Email = reader.IsDBNull(reader.GetOrdinal("email"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("email")),

                    Direccion = reader.IsDBNull(reader.GetOrdinal("direccion"))
                        ? null
                        : reader.GetString(reader.GetOrdinal("direccion")),

                    FechaRegistro = reader.GetDateTime(reader.GetOrdinal("fecha_registro")),
                    Estado = reader.GetBoolean(reader.GetOrdinal("estado"))
                };
            }

            if (cliente == null)
            {
                return NotFound();
            }

            return View(cliente);
        }


        // HU-29: Mostrar formulario de registro
        [HttpGet]
        public IActionResult Crear()
        {
            return View();
        }

        // HU-30: Mostrar formulario de edición
        [HttpGet]
        public IActionResult Editar(int id)
        {
            Cliente? cliente = null;

            using var connection =
                (NpgsqlConnection)_dbConnection.CreateConnection();

            connection.Open();

            using var command = new NpgsqlCommand(
                @"SELECT id_cliente, nombre, apellido, identificacion,
                 telefono, email, direccion
          FROM public.cliente
          WHERE id_cliente = @id;",
                connection);

            command.Parameters.AddWithValue("id", id);

            using var reader = command.ExecuteReader();

            if (reader.Read())
            {
                cliente = new Cliente
                {
                    IdCliente = reader.GetInt32(
                        reader.GetOrdinal("id_cliente")),

                    Nombre = reader.GetString(
                        reader.GetOrdinal("nombre")),

                    Apellido = reader.GetString(
                        reader.GetOrdinal("apellido")),

                    Identificacion = reader.IsDBNull(
                        reader.GetOrdinal("identificacion"))
                            ? null
                            : reader.GetString(
                                reader.GetOrdinal("identificacion")),

                    Telefono = reader.IsDBNull(
                        reader.GetOrdinal("telefono"))
                            ? null
                            : reader.GetString(
                                reader.GetOrdinal("telefono")),

                    Email = reader.IsDBNull(
                        reader.GetOrdinal("email"))
                            ? null
                            : reader.GetString(
                                reader.GetOrdinal("email")),

                    Direccion = reader.IsDBNull(
                        reader.GetOrdinal("direccion"))
                            ? null
                            : reader.GetString(
                                reader.GetOrdinal("direccion"))
                };
            }

            if (cliente == null)
            {
                return NotFound();
            }

            return View(cliente);
        }
        
        // HU-30: Guardar los cambios de un cliente
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Editar(Cliente modelo)
                {
                    if (!ModelState.IsValid)
                    {
                        return View(modelo);
                    }

                    using var connection =
                        (NpgsqlConnection)_dbConnection.CreateConnection();

                    connection.Open();

                    using var command = new NpgsqlCommand(
                        @"UPDATE public.cliente
                          SET nombre = @nombre,
                              apellido = @apellido,
                              identificacion = @identificacion,
                              telefono = @telefono,
                              email = @email,
                              direccion = @direccion,
                              estado = @estado
                          WHERE id_cliente = @id;",
                        connection);

                    command.Parameters.AddWithValue(
                        "id", modelo.IdCliente);

                    command.Parameters.AddWithValue(
                        "nombre", modelo.Nombre.Trim());

                    command.Parameters.AddWithValue(
                        "apellido", modelo.Apellido.Trim());

                    command.Parameters.AddWithValue(
                        "identificacion",
                        (object?)TextoOpcional(modelo.Identificacion) ?? DBNull.Value);

                    command.Parameters.AddWithValue(
                        "telefono",
                        (object?)TextoOpcional(modelo.Telefono) ?? DBNull.Value);

                    command.Parameters.AddWithValue(
                        "email",
                        (object?)TextoOpcional(modelo.Email) ?? DBNull.Value);

                    command.Parameters.AddWithValue(
                        "direccion",
                        (object?)TextoOpcional(modelo.Direccion) ?? DBNull.Value);

                    command.Parameters.AddWithValue("estado", modelo.Estado);

                    try
                    {
                        int filasAfectadas = command.ExecuteNonQuery();

                        if (filasAfectadas == 0)
                        {
                            return NotFound();
                        }

                        TempData["Success"] =
                            "La información del cliente se actualizó correctamente.";

                        return RedirectToAction(
                            nameof(Detalle),
                            new { id = modelo.IdCliente });
                    }
                    catch (PostgresException ex)
                        when (ex.SqlState == "23505")
                    {
                        ModelState.AddModelError(
                            "",
                            "La identificación o el teléfono ya pertenecen a otro cliente.");

                        return View(modelo);
                    }
                }

        // HU-29: Registrar un cliente
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Crear(CrearClienteViewModel modelo)
        {
            if (!ModelState.IsValid)
            {
                return View(modelo);
            }

            using var connection =
                (NpgsqlConnection)_dbConnection.CreateConnection();

            connection.Open();

            using var command = new NpgsqlCommand(
                @"CALL public.sp_crear_cliente(
                    @p_nombre,
                    @p_apellido,
                    @p_identificacion,
                    @p_telefono,
                    @p_email,
                    @p_direccion,
                    @p_resultado
                );",
                connection);

            command.Parameters.AddWithValue(
                "p_nombre", modelo.Nombre.Trim());

            command.Parameters.AddWithValue(
                "p_apellido", modelo.Apellido.Trim());

            command.Parameters.AddWithValue(
                "p_identificacion",
                (object?)TextoOpcional(modelo.Identificacion) ?? DBNull.Value);

            command.Parameters.AddWithValue(
                "p_telefono",
                (object?)TextoOpcional(modelo.Telefono) ?? DBNull.Value);

            command.Parameters.AddWithValue(
                "p_email",
                (object?)TextoOpcional(modelo.Email) ?? DBNull.Value);

            command.Parameters.AddWithValue(
                "p_direccion",
                (object?)TextoOpcional(modelo.Direccion) ?? DBNull.Value);

            var resultado = new NpgsqlParameter(
                "p_resultado", NpgsqlDbType.Varchar)
            {
                Size = 30,
                Direction = ParameterDirection.InputOutput,
                Value = DBNull.Value
            };

            command.Parameters.Add(resultado);
            command.ExecuteNonQuery();

            switch (resultado.Value?.ToString())
            {
                case "CREADO":
                    TempData["Success"] =
                        "El cliente fue registrado correctamente.";

                    return RedirectToAction(nameof(Index));

                case "IDENTIFICACION_EXISTENTE":
                    ModelState.AddModelError(
                        nameof(modelo.Identificacion),
                        "Ya existe un cliente con esa identificación.");
                    break;

                case "TELEFONO_EXISTENTE":
                    ModelState.AddModelError(
                        nameof(modelo.Telefono),
                        "Ya existe un cliente con ese teléfono.");
                    break;

                default:
                    ModelState.AddModelError(
                        "",
                        "No fue posible registrar el cliente. Verifique los datos.");
                    break;
            }

            return View(modelo);
        }

        // HU-35: Cambiar estado de un cliente
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CambiarEstado(int id)
        {
            using var connection =
                (NpgsqlConnection)_dbConnection.CreateConnection();

            connection.Open();

            using var command = new NpgsqlCommand(
                @"UPDATE public.cliente
          SET estado = NOT estado
          WHERE id_cliente = @id;",
                connection);

            command.Parameters.AddWithValue("id", id);

            int filasAfectadas = command.ExecuteNonQuery();

            if (filasAfectadas == 0)
            {
                return NotFound();
            }

            TempData["Success"] =
                "El estado del cliente se actualizó correctamente.";

            return RedirectToAction(nameof(Index));
        }

        // Obtiene los clientes reales desde PostgreSQL.
        private List<Cliente> ObtenerClientes()
        {
            var clientes = new List<Cliente>();

            using var connection =
                (NpgsqlConnection)_dbConnection.CreateConnection();

            connection.Open();

            using var transaction = connection.BeginTransaction();

            using (var command = new NpgsqlCommand(
                "CALL public.sp_obtener_clientes(@p_clientes);",
                connection,
                transaction))
            {
                var cursor = new NpgsqlParameter(
                    "p_clientes", NpgsqlDbType.Refcursor)
                {
                    Direction = ParameterDirection.InputOutput,
                    Value = "clientes_cursor"
                };

                command.Parameters.Add(cursor);
                command.ExecuteNonQuery();
            }

            using (var command = new NpgsqlCommand(
                "FETCH ALL FROM clientes_cursor;",
                connection,
                transaction))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    clientes.Add(new Cliente
                    {
                        IdCliente = reader.GetInt32(
                            reader.GetOrdinal("id_cliente")),

                        Nombre = reader.GetString(
                            reader.GetOrdinal("nombre")),

                        Apellido = reader.GetString(
                            reader.GetOrdinal("apellido")),

                        Identificacion = reader.IsDBNull(
                            reader.GetOrdinal("identificacion"))
                                ? null
                                : reader.GetString(
                                    reader.GetOrdinal("identificacion")),

                        Telefono = reader.IsDBNull(
                            reader.GetOrdinal("telefono"))
                                ? null
                                : reader.GetString(
                                    reader.GetOrdinal("telefono")),

                        Email = reader.IsDBNull(
                            reader.GetOrdinal("email"))
                                ? null
                                : reader.GetString(
                                    reader.GetOrdinal("email")),

                        Direccion = reader.IsDBNull(
                            reader.GetOrdinal("direccion"))
                                ? null
                                : reader.GetString(
                                    reader.GetOrdinal("direccion")),

                        FechaRegistro = reader.GetDateTime(
                            reader.GetOrdinal("fecha_registro")),

                        Estado = reader.GetBoolean(
                            reader.GetOrdinal("estado"))
                    });
                }
            }

            transaction.Commit();

            return clientes;
        }

        // Convierte textos vacíos en null para PostgreSQL.
        private static string? TextoOpcional(string? valor)
        {
            return string.IsNullOrWhiteSpace(valor)
                ? null
                : valor.Trim();
        }
    }
}
