using CharlieShop.Data;
using CharlieShop.Models;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Data;
using System.Text.Json;

namespace CharlieShop.Controllers
{
    [Authorize]
    public class UsuariosController : Controller
    {
        private readonly DbConnection _dbConnection;
        private readonly PasswordHasher<Usuario> _passwordHasher;

        public UsuariosController(DbConnection dbConnection)
        {
            _dbConnection = dbConnection;
            _passwordHasher = new PasswordHasher<Usuario>();
        }

        // =========================================
        // PÁGINA PRINCIPAL DE USUARIOS
        // =========================================

        public async Task<IActionResult> Index()
        {
            try
            {
                using var connection =
                    _dbConnection.CreateConnection();

                var parametros =
                    new DynamicParameters();

                parametros.Add(
                    "o_usuarios",
                    dbType: DbType.String,
                    direction: ParameterDirection.Output);

                await connection.ExecuteAsync(
                    "sp_obtener_usuarios",
                    parametros,
                    commandType: CommandType.StoredProcedure);

                string jsonUsuarios =
                    parametros.Get<string>("o_usuarios");

                var usuarios =
                    JsonSerializer.Deserialize<List<Usuario>>(
                        jsonUsuarios)
                    ?? new List<Usuario>();

                return View(usuarios);
            }
            catch (PostgresException)
            {
                TempData["Error"] =
                    "No fue posible obtener la lista de usuarios.";

                return View(new List<Usuario>());
            }
            catch (NpgsqlException)
            {
                TempData["Error"] =
                    "No fue posible conectar con la base de datos.";

                return View(new List<Usuario>());
            }
            catch (Exception)
            {
                TempData["Error"] =
                    "Ocurrió un error inesperado al cargar los usuarios.";

                return View(new List<Usuario>());
            }
        }

        // =========================================
        // DESACTIVAR/ACTIVAR USUARIO - POST
        // =========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarEstado(int id, bool estado)
        {
            try
            {
                using var connection = _dbConnection.CreateConnection();

                var parametros = new DynamicParameters();

                parametros.Add(
                    "p_id_usuario",
                    id,
                    DbType.Int32,
                    ParameterDirection.Input);

                parametros.Add(
                    "p_estado",
                    estado,
                    DbType.Boolean,
                    ParameterDirection.Input);

                await connection.ExecuteAsync(
                    "sp_cambiar_estado_usuario",
                    parametros,
                    commandType: CommandType.StoredProcedure);

                TempData["Mensaje"] = estado
                    ? "El usuario fue activado correctamente."
                    : "El usuario fue desactivado correctamente.";

                return RedirectToAction(nameof(Index));
            }
            catch (PostgresException)
            {
                TempData["Error"] =
                    "No fue posible cambiar el estado del usuario.";

                return RedirectToAction(nameof(Index));
            }
            catch (NpgsqlException)
            {
                TempData["Error"] =
                    "No fue posible conectar con la base de datos.";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception)
            {
                TempData["Error"] =
                    "Ocurrió un error inesperado al cambiar el estado del usuario.";

                return RedirectToAction(nameof(Index));
            }
        }

        // =========================================
        // EDITAR USUARIO - GET
        // =========================================

        [HttpGet]
        public async Task<IActionResult> Editar(int id)
        {
            try
            {
                using var connection =
                    _dbConnection.CreateConnection();

                // -----------------------------------------
                // OBTENER USUARIO
                // -----------------------------------------

                var parametrosUsuario =
                    new DynamicParameters();

                parametrosUsuario.Add(
                    "p_id_usuario",
                    id,
                    DbType.Int32,
                    ParameterDirection.Input);

                parametrosUsuario.Add(
                    "o_usuario",
                    dbType: DbType.String,
                    direction: ParameterDirection.Output);

                await connection.ExecuteAsync(
                    "sp_obtener_usuario_editar",
                    parametrosUsuario,
                    commandType: CommandType.StoredProcedure);

                string? jsonUsuario =
                    parametrosUsuario.Get<string?>("o_usuario");

                if (string.IsNullOrWhiteSpace(jsonUsuario))
                {
                    TempData["Error"] =
                        "El usuario que intenta editar no existe.";

                    return RedirectToAction(nameof(Index));
                }

                var modelo =
                    JsonSerializer.Deserialize<EditarUsuarioViewModel>(
                        jsonUsuario);

                if (modelo == null)
                {
                    TempData["Error"] =
                        "No fue posible cargar la información del usuario.";

                    return RedirectToAction(nameof(Index));
                }


                // -----------------------------------------
                // OBTENER ROLES
                // -----------------------------------------

                var parametrosRoles =
                    new DynamicParameters();

                parametrosRoles.Add(
                    "o_roles",
                    dbType: DbType.String,
                    direction: ParameterDirection.Output);

                await connection.ExecuteAsync(
                    "sp_obtener_roles_activos",
                    parametrosRoles,
                    commandType: CommandType.StoredProcedure);

                string jsonRoles =
                    parametrosRoles.Get<string>("o_roles");

                modelo.Roles =
                    JsonSerializer.Deserialize<List<RolOpcionViewModel>>(
                        jsonRoles)
                    ?? new List<RolOpcionViewModel>();


                return View(modelo);
            }
            catch (PostgresException)
            {
                TempData["Error"] =
                    "No fue posible cargar la información del usuario.";

                return RedirectToAction(nameof(Index));
            }
            catch (NpgsqlException)
            {
                TempData["Error"] =
                    "No fue posible conectar con la base de datos.";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception)
            {
                TempData["Error"] =
                    "Ocurrió un error inesperado.";

                return RedirectToAction(nameof(Index));
            }
        }

        // =========================================
        // EDITAR USUARIO - POST
        // =========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(
            EditarUsuarioViewModel modelo)
        {
            if (!ModelState.IsValid)
            {
                await CargarRoles(modelo);

                return View(modelo);
            }

            try
            {
                using var connection =
                    _dbConnection.CreateConnection();


                // -----------------------------------------
                // NORMALIZAR DATOS
                // -----------------------------------------

                modelo.Nombre =
                    modelo.Nombre.Trim();

                modelo.Usuario =
                    modelo.Usuario.Trim();

                modelo.Email =
                    modelo.Email.Trim().ToLower();


                // -----------------------------------------
                // VERIFICAR USUARIO DUPLICADO
                // -----------------------------------------

                var parametrosUsuario =
                    new DynamicParameters();

                parametrosUsuario.Add(
                    "p_id_usuario",
                    modelo.IdUsuario,
                    DbType.Int32,
                    ParameterDirection.Input);

                parametrosUsuario.Add(
                    "p_usuario",
                    modelo.Usuario,
                    DbType.String,
                    ParameterDirection.Input);

                parametrosUsuario.Add(
                    "o_existe",
                    dbType: DbType.Boolean,
                    direction: ParameterDirection.Output);

                await connection.ExecuteAsync(
                    "sp_verificar_usuario_editar",
                    parametrosUsuario,
                    commandType: CommandType.StoredProcedure);

                bool usuarioExiste =
                    parametrosUsuario.Get<bool>("o_existe");

                if (usuarioExiste)
                {
                    ModelState.AddModelError(
                        "Usuario",
                        "El nombre de usuario ya está registrado.");

                    await CargarRoles(modelo);

                    return View(modelo);
                }


                // -----------------------------------------
                // VERIFICAR EMAIL DUPLICADO
                // -----------------------------------------

                var parametrosEmail =
                    new DynamicParameters();

                parametrosEmail.Add(
                    "p_id_usuario",
                    modelo.IdUsuario,
                    DbType.Int32,
                    ParameterDirection.Input);

                parametrosEmail.Add(
                    "p_email",
                    modelo.Email,
                    DbType.String,
                    ParameterDirection.Input);

                parametrosEmail.Add(
                    "o_existe",
                    dbType: DbType.Boolean,
                    direction: ParameterDirection.Output);

                await connection.ExecuteAsync(
                    "sp_verificar_email_editar",
                    parametrosEmail,
                    commandType: CommandType.StoredProcedure);

                bool emailExiste =
                    parametrosEmail.Get<bool>("o_existe");

                if (emailExiste)
                {
                    ModelState.AddModelError(
                        "Email",
                        "El correo electrónico ya está registrado.");

                    await CargarRoles(modelo);

                    return View(modelo);
                }


                // -----------------------------------------
                // ACTUALIZAR USUARIO
                // -----------------------------------------

                var parametrosActualizar =
                    new DynamicParameters();

                parametrosActualizar.Add(
                    "p_id_usuario",
                    modelo.IdUsuario,
                    DbType.Int32,
                    ParameterDirection.Input);

                parametrosActualizar.Add(
                    "p_nombre",
                    modelo.Nombre,
                    DbType.String,
                    ParameterDirection.Input);

                parametrosActualizar.Add(
                    "p_usuario",
                    modelo.Usuario,
                    DbType.String,
                    ParameterDirection.Input);

                parametrosActualizar.Add(
                    "p_email",
                    modelo.Email,
                    DbType.String,
                    ParameterDirection.Input);

                parametrosActualizar.Add(
                    "p_rol_id",
                    modelo.RolId,
                    DbType.Int32,
                    ParameterDirection.Input);

                parametrosActualizar.Add(
                    "p_estado",
                    modelo.Estado,
                    DbType.Boolean,
                    ParameterDirection.Input);

                await connection.ExecuteAsync(
                    "sp_actualizar_usuario",
                    parametrosActualizar,
                    commandType: CommandType.StoredProcedure);


                TempData["Mensaje"] =
                    "Los datos del usuario fueron actualizados correctamente.";

                return RedirectToAction(nameof(Index));
            }
            catch (PostgresException)
            {
                ModelState.AddModelError(
                    "",
                    "No fue posible actualizar el usuario. Intente nuevamente.");

                await CargarRoles(modelo);

                return View(modelo);
            }
            catch (NpgsqlException)
            {
                ModelState.AddModelError(
                    "",
                    "No fue posible conectar con la base de datos.");

                await CargarRoles(modelo);

                return View(modelo);
            }
            catch (Exception)
            {
                ModelState.AddModelError(
                    "",
                    "Ocurrió un error inesperado al actualizar el usuario.");

                await CargarRoles(modelo);

                return View(modelo);
            }
        }

        // =========================================
        // CREAR USUARIO - GET
        // =========================================

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Crear()
        {
            return View();
        }

        // =========================================
        // PERFIL DE USUARIO - GET
        // =========================================

        [HttpGet]
        public IActionResult Perfil()
        {
            return View();
        }

        // =========================================
        // CREAR USUARIO - POST
        // =========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public IActionResult Crear(CrearUsuarioViewModel modelo)
        {
            // -----------------------------------------
            // VALIDACIONES DEL MODELO
            // -----------------------------------------

            if (!ModelState.IsValid)
            {
                return View(modelo);
            }

            try
            {
                using var connection =
                    _dbConnection.CreateConnection();

                // -----------------------------------------
                // NORMALIZAR DATOS
                // -----------------------------------------

                modelo.Nombre = modelo.Nombre.Trim();
                modelo.Usuario = modelo.Usuario.Trim();
                modelo.Email = modelo.Email.Trim().ToLower();

                // -----------------------------------------
                // VERIFICAR USUARIO EXISTENTE
                // -----------------------------------------

                var parametrosUsuario =
                    new DynamicParameters();

                parametrosUsuario.Add(
                    "p_usuario",
                    modelo.Usuario,
                    DbType.String,
                    ParameterDirection.Input);

                parametrosUsuario.Add(
                    "o_existe",
                    dbType: DbType.Boolean,
                    direction: ParameterDirection.Output);

                connection.Execute(
                    "sp_verificar_usuario_existente",
                    parametrosUsuario,
                    commandType: CommandType.StoredProcedure);

                bool usuarioExiste =
                    parametrosUsuario.Get<bool>("o_existe");

                if (usuarioExiste)
                {
                    ModelState.AddModelError(
                        "Usuario",
                        "El nombre de usuario ya está registrado.");

                    return View(modelo);
                }

                // -----------------------------------------
                // VERIFICAR EMAIL EXISTENTE
                // -----------------------------------------

                var parametrosEmail =
                    new DynamicParameters();

                parametrosEmail.Add(
                    "p_email",
                    modelo.Email,
                    DbType.String,
                    ParameterDirection.Input);

                parametrosEmail.Add(
                    "o_existe",
                    dbType: DbType.Boolean,
                    direction: ParameterDirection.Output);

                connection.Execute(
                    "sp_verificar_email_existente",
                    parametrosEmail,
                    commandType: CommandType.StoredProcedure);

                bool emailExiste =
                    parametrosEmail.Get<bool>("o_existe");

                if (emailExiste)
                {
                    ModelState.AddModelError(
                        "Email",
                        "El correo electrónico ya está registrado.");

                    return View(modelo);
                }

                // -----------------------------------------
                // OBTENER ROL GUEST
                // -----------------------------------------

                var parametrosRol =
                    new DynamicParameters();

                parametrosRol.Add(
                    "o_id_rol",
                    dbType: DbType.Int32,
                    direction: ParameterDirection.Output);

                connection.Execute(
                    "sp_obtener_rol_guest",
                    parametrosRol,
                    commandType: CommandType.StoredProcedure);

                int? rolId =
                    parametrosRol.Get<int?>("o_id_rol");

                if (rolId == null)
                {
                    ModelState.AddModelError(
                        "",
                        "No fue posible asignar el rol del usuario.");

                    return View(modelo);
                }

                // -----------------------------------------
                // CREAR OBJETO USUARIO
                // -----------------------------------------

                var usuario = new Usuario
                {
                    Nombre = modelo.Nombre,
                    UsuarioNombre = modelo.Usuario,
                    Email = modelo.Email,
                    RolId = rolId.Value,
                    Estado = true
                };

                // -----------------------------------------
                // GENERAR HASH DE CONTRASEÑA
                // -----------------------------------------

                usuario.ContrasenaHash =
                    _passwordHasher.HashPassword(
                        usuario,
                        modelo.Contrasena);

                // -----------------------------------------
                // INSERTAR USUARIO
                // -----------------------------------------

                var parametrosCrear =
                    new DynamicParameters();

                parametrosCrear.Add(
                    "p_nombre",
                    usuario.Nombre,
                    DbType.String,
                    ParameterDirection.Input);

                parametrosCrear.Add(
                    "p_usuario",
                    usuario.UsuarioNombre,
                    DbType.String,
                    ParameterDirection.Input);

                parametrosCrear.Add(
                    "p_contrasena_hash",
                    usuario.ContrasenaHash,
                    DbType.String,
                    ParameterDirection.Input);

                parametrosCrear.Add(
                    "p_email",
                    usuario.Email,
                    DbType.String,
                    ParameterDirection.Input);

                parametrosCrear.Add(
                    "p_rol_id",
                    usuario.RolId,
                    DbType.Int32,
                    ParameterDirection.Input);

                parametrosCrear.Add(
                    "p_estado",
                    usuario.Estado,
                    DbType.Boolean,
                    ParameterDirection.Input);

                connection.Execute(
                    "sp_crear_usuario",
                    parametrosCrear,
                    commandType: CommandType.StoredProcedure);

                // -----------------------------------------
                // REGISTRO EXITOSO
                // -----------------------------------------

                TempData["Mensaje"] =
                    "La cuenta fue creada correctamente.";

                return RedirectToAction(
                    "Index",
                    "Home");
            }
            catch (PostgresException)
            {
                ModelState.AddModelError(
                    "",
                    "No fue posible completar el registro. Intente nuevamente.");

                return View(modelo);
            }
            catch (NpgsqlException)
            {
                ModelState.AddModelError(
                    "",
                    "No fue posible conectar con la base de datos. Intente nuevamente.");

                return View(modelo);
            }
            catch (Exception)
            {
                ModelState.AddModelError(
                    "",
                    "Ocurrió un error inesperado al crear la cuenta.");

                return View(modelo);
            }
        }

        // =========================================
        // CAMBIAR CONTRASEÑA
        // =========================================

        [HttpGet]
        public IActionResult CambiarContrasena()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CambiarContrasena(
            CambiarContrasenaViewModel modelo)
        {
            if (!ModelState.IsValid)
            {
                return View(modelo);
            }

            try
            {
                using var connection =
                    _dbConnection.CreateConnection();

                // Obtener el usuario actualmente autenticado
                string? nombreUsuario =
                    User.Identity?.Name;

                if (string.IsNullOrWhiteSpace(nombreUsuario))
                {
                    ModelState.AddModelError(
                        "",
                        "No fue posible identificar la cuenta actual.");

                    return View(modelo);
                }

                // -----------------------------------------
                // BUSCAR USUARIO Y CONTRASEÑA ACTUAL
                // -----------------------------------------

                var parametrosUsuario =
                    new DynamicParameters();

                parametrosUsuario.Add(
                    "p_usuario",
                    nombreUsuario,
                    DbType.String,
                    ParameterDirection.Input);

                parametrosUsuario.Add(
                    "o_id_usuario",
                    dbType: DbType.Int32,
                    direction: ParameterDirection.Output);

                parametrosUsuario.Add(
                    "o_nombre",
                    dbType: DbType.String,
                    direction: ParameterDirection.Output);

                parametrosUsuario.Add(
                    "o_usuario_nombre",
                    dbType: DbType.String,
                    direction: ParameterDirection.Output);

                parametrosUsuario.Add(
                    "o_contrasena_hash",
                    dbType: DbType.String,
                    direction: ParameterDirection.Output);

                parametrosUsuario.Add(
                    "o_email",
                    dbType: DbType.String,
                    direction: ParameterDirection.Output);

                parametrosUsuario.Add(
                    "o_rol_id",
                    dbType: DbType.Int32,
                    direction: ParameterDirection.Output);

                parametrosUsuario.Add(
                    "o_estado",
                    dbType: DbType.Boolean,
                    direction: ParameterDirection.Output);

                parametrosUsuario.Add(
                    "o_fecha_creacion",
                    dbType: DbType.DateTime,
                    direction: ParameterDirection.Output);

                connection.Execute(
                    "sp_obtener_usuario_contrasena",
                    parametrosUsuario,
                    commandType: CommandType.StoredProcedure);

                int? idUsuario =
                    parametrosUsuario.Get<int?>("o_id_usuario");

                if (!idUsuario.HasValue)
                {
                    ModelState.AddModelError(
                        "",
                        "No fue posible encontrar la cuenta actual.");

                    return View(modelo);
                }

                var usuario = new Usuario
                {
                    IdUsuario = idUsuario.Value,
                    Nombre = parametrosUsuario.Get<string?>("o_nombre") ?? string.Empty,
                    UsuarioNombre = parametrosUsuario.Get<string?>("o_usuario_nombre") ?? string.Empty,
                    ContrasenaHash = parametrosUsuario.Get<string?>("o_contrasena_hash") ?? string.Empty,
                    Email = parametrosUsuario.Get<string?>("o_email"),
                    RolId = parametrosUsuario.Get<int?>("o_rol_id") ?? 0,
                    Estado = parametrosUsuario.Get<bool?>("o_estado") ?? false,
                    FechaCreacion = parametrosUsuario.Get<DateTime?>("o_fecha_creacion") ?? DateTime.MinValue
                };

                // -----------------------------------------
                // VERIFICAR CONTRASEÑA ACTUAL
                // -----------------------------------------

                var resultadoVerificacion =
                    _passwordHasher.VerifyHashedPassword(
                        usuario,
                        usuario.ContrasenaHash,
                        modelo.ContrasenaActual);

                if (resultadoVerificacion ==
                    PasswordVerificationResult.Failed)
                {
                    ModelState.AddModelError(
                        "ContrasenaActual",
                        "La contraseña actual no es correcta.");

                    return View(modelo);
                }

                // -----------------------------------------
                // GENERAR NUEVO HASH
                // -----------------------------------------

                string nuevaContrasenaHash =
                    _passwordHasher.HashPassword(
                        usuario,
                        modelo.NuevaContrasena);

                // -----------------------------------------
                // ACTUALIZAR CONTRASEÑA
                // -----------------------------------------

                var parametrosActualizar =
                    new DynamicParameters();

                parametrosActualizar.Add(
                    "p_id_usuario",
                    usuario.IdUsuario,
                    DbType.Int32,
                    ParameterDirection.Input);

                parametrosActualizar.Add(
                    "p_contrasena_hash",
                    nuevaContrasenaHash,
                    DbType.String,
                    ParameterDirection.Input);

                connection.Execute(
                    "sp_actualizar_contrasena_usuario",
                    parametrosActualizar,
                    commandType: CommandType.StoredProcedure);

                TempData["Mensaje"] =
                    "La contraseña fue actualizada correctamente.";

                return RedirectToAction(nameof(Perfil));
            }
            catch (PostgresException)
            {
                ModelState.AddModelError(
                    "",
                    "No fue posible actualizar la contraseña. Intente nuevamente.");

                return View(modelo);
            }
            catch (NpgsqlException)
            {
                ModelState.AddModelError(
                    "",
                    "No fue posible conectar con la base de datos. Intente nuevamente.");

                return View(modelo);
            }
            catch (Exception)
            {
                ModelState.AddModelError(
                    "",
                    "Ocurrió un error inesperado al actualizar la contraseña.");

                return View(modelo);
            }
        }

        // =========================================
        // CAMBIAR CONTRASEÑA DE USUARIO
        // =========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarContrasenaUsuario(
            CambiarContrasenaUsuarioViewModel modelo)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] =
                    "La contraseña no cumple con los requisitos de seguridad.\r\n\r\nDebe tener un mínimo de 8 caracteres. Se recomienda utilizar una combinación de letras mayúsculas, minúsculas y números.\r\n";

                return RedirectToAction(nameof(Index));
            }

            try
            {
                using var connection =
                    _dbConnection.CreateConnection();

                // -----------------------------------------
                // VERIFICAR QUE EL USUARIO EXISTA
                // -----------------------------------------

                var parametrosUsuario =
                    new DynamicParameters();

                parametrosUsuario.Add(
                    "p_id_usuario",
                    modelo.IdUsuario,
                    DbType.Int32,
                    ParameterDirection.Input);

                parametrosUsuario.Add(
                    "o_usuario",
                    dbType: DbType.String,
                    direction: ParameterDirection.Output);

                await connection.ExecuteAsync(
                    "sp_obtener_usuario_editar",
                    parametrosUsuario,
                    commandType: CommandType.StoredProcedure);

                string? jsonUsuario =
                    parametrosUsuario.Get<string?>("o_usuario");

                if (string.IsNullOrWhiteSpace(jsonUsuario))
                {
                    TempData["Error"] =
                        "El usuario seleccionado no existe.";

                    return RedirectToAction(nameof(Index));
                }

                var datosUsuario =
                    JsonSerializer.Deserialize<EditarUsuarioViewModel>(
                        jsonUsuario);

                if (datosUsuario == null)
                {
                    TempData["Error"] =
                        "No fue posible obtener la información del usuario.";

                    return RedirectToAction(nameof(Index));
                }

                // -----------------------------------------
                // CREAR OBJETO USUARIO
                // -----------------------------------------

                var usuario = new Usuario
                {
                    IdUsuario = datosUsuario.IdUsuario,
                    Nombre = datosUsuario.Nombre,
                    UsuarioNombre = datosUsuario.Usuario,
                    Email = datosUsuario.Email,
                    RolId = datosUsuario.RolId,
                    Estado = datosUsuario.Estado
                };

                // -----------------------------------------
                // GENERAR HASH DE LA NUEVA CONTRASEÑA
                // -----------------------------------------

                string nuevaContrasenaHash =
                    _passwordHasher.HashPassword(
                        usuario,
                        modelo.NuevaContrasena);

                // -----------------------------------------
                // ACTUALIZAR CONTRASEÑA
                // -----------------------------------------

                var parametrosActualizar =
                    new DynamicParameters();

                parametrosActualizar.Add(
                    "p_id_usuario",
                    modelo.IdUsuario,
                    DbType.Int32,
                    ParameterDirection.Input);

                parametrosActualizar.Add(
                    "p_contrasena_hash",
                    nuevaContrasenaHash,
                    DbType.String,
                    ParameterDirection.Input);

                await connection.ExecuteAsync(
                    "sp_actualizar_contrasena_usuario",
                    parametrosActualizar,
                    commandType: CommandType.StoredProcedure);

                // -----------------------------------------
                // ÉXITO
                // -----------------------------------------

                TempData["Mensaje"] =
                    "La contraseña del usuario fue actualizada correctamente.";

                return RedirectToAction(nameof(Index));
            }
            catch (PostgresException)
            {
                TempData["Error"] =
                    "No fue posible actualizar la contraseña debido a un problema con la base de datos.";

                return RedirectToAction(nameof(Index));
            }
            catch (NpgsqlException)
            {
                TempData["Error"] =
                    "No fue posible conectar con la base de datos.";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception)
            {
                TempData["Error"] =
                    "Ocurrió un error inesperado al actualizar la contraseña.";

                return RedirectToAction(nameof(Index));
            }
        }

        private async Task CargarRoles(
            EditarUsuarioViewModel modelo)
        {
            using var connection =
                _dbConnection.CreateConnection();

            var parametros =
                new DynamicParameters();

            parametros.Add(
                "o_roles",
                dbType: DbType.String,
                direction: ParameterDirection.Output);

            await connection.ExecuteAsync(
                "sp_obtener_roles_activos",
                parametros,
                commandType: CommandType.StoredProcedure);

            string jsonRoles =
                parametros.Get<string>("o_roles");

            modelo.Roles =
                JsonSerializer.Deserialize<List<RolOpcionViewModel>>(
                    jsonRoles)
                ?? new List<RolOpcionViewModel>();
        }
    }
}
