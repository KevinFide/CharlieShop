using CharlieShop.Data;
using CharlieShop.Models;
using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace CharlieShop.Controllers
{
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

        public IActionResult Index()
        {
            return View();
        }


        // =========================================
        // CREAR USUARIO - GET
        // =========================================

        [HttpGet]
        public IActionResult Crear()
        {
            return View();
        }


        // =========================================
        // CREAR USUARIO - POST
        // =========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
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
                using var connection = _dbConnection.CreateConnection();


                // -----------------------------------------
                // NORMALIZAR DATOS
                // -----------------------------------------

                modelo.Nombre = modelo.Nombre.Trim();
                modelo.Usuario = modelo.Usuario.Trim();
                modelo.Email = modelo.Email.Trim().ToLower();


                // -----------------------------------------
                // VERIFICAR USUARIO EXISTENTE
                // -----------------------------------------

                const string verificarUsuario = """
                    SELECT COUNT(*)
                    FROM usuario
                    WHERE usuario = @Usuario
                """;

                long usuarioExiste = connection.ExecuteScalar<long>(
                    verificarUsuario,
                    new
                    {
                        Usuario = modelo.Usuario
                    });


                if (usuarioExiste > 0)
                {
                    ModelState.AddModelError(
                        "Usuario",
                        "El nombre de usuario ya está registrado."
                    );

                    return View(modelo);
                }


                // -----------------------------------------
                // VERIFICAR EMAIL EXISTENTE
                // -----------------------------------------

                const string verificarEmail = """
                    SELECT COUNT(*)
                    FROM usuario
                    WHERE LOWER(email) = LOWER(@Email)
                """;

                long emailExiste = connection.ExecuteScalar<long>(
                    verificarEmail,
                    new
                    {
                        Email = modelo.Email
                    });


                if (emailExiste > 0)
                {
                    ModelState.AddModelError(
                        "Email",
                        "El correo electrónico ya está registrado."
                    );

                    return View(modelo);
                }


                // -----------------------------------------
                // OBTENER ROL VENDEDOR
                // -----------------------------------------

                const string obtenerRol = """
                    SELECT id_rol
                    FROM rol
                    WHERE nombre = 'GUEST'
                      AND estado = TRUE
                """;

                int? rolId = connection.ExecuteScalar<int?>(
                    obtenerRol
                );


                if (rolId == null)
                {
                    ModelState.AddModelError(
                        "",
                        "No fue posible asignar el rol del usuario."
                    );

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
                        modelo.Contrasena
                    );


                // -----------------------------------------
                // INSERTAR USUARIO
                // -----------------------------------------

                const string insertarUsuario = """
                    INSERT INTO usuario
                    (
                        nombre,
                        usuario,
                        contrasena_hash,
                        email,
                        rol_id,
                        estado
                    )
                    VALUES
                    (
                        @Nombre,
                        @UsuarioNombre,
                        @ContrasenaHash,
                        @Email,
                        @RolId,
                        @Estado
                    )
                """;


                connection.Execute(
                    insertarUsuario,
                    usuario
                );


                // -----------------------------------------
                // REGISTRO EXITOSO
                // -----------------------------------------

                TempData["Mensaje"] =
                    "La cuenta fue creada correctamente.";

                return RedirectToAction(
                    "Index",
                    "Home"
                );
            }


            // =========================================
            // ERROR DE POSTGRESQL
            // =========================================

            catch (PostgresException)
            {
                ModelState.AddModelError(
                    "",
                    "No fue posible completar el registro. Intente nuevamente."
                );

                return View(modelo);
            }


            // =========================================
            // ERROR DE CONEXIÓN / NPGSQL
            // =========================================

            catch (NpgsqlException)
            {
                ModelState.AddModelError(
                    "",
                    "No fue posible conectar con la base de datos. Intente nuevamente."
                );

                return View(modelo);
            }


            // =========================================
            // ERROR INESPERADO
            // =========================================

            catch (Exception)
            {
                ModelState.AddModelError(
                    "",
                    "Ocurrió un error inesperado al crear la cuenta."
                );

                return View(modelo);
            }
        }


        // =========================================
        // CAMBIAR CONTRASEÑA
        // =========================================

        public IActionResult CambiarContrasena()
        {
            return View();
        }
    }
}