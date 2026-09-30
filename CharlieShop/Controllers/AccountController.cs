using CharlieShop.Data;
using CharlieShop.Models;
using CharlieShop.Services;
using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;

namespace CharlieShop.Controllers
{
    public class AccountController : Controller
    {
        private readonly DbConnection _dbConnection;
        private readonly PasswordHasher<Usuario> _passwordHasher;
        private readonly EmailService _emailService;

        public AccountController(
            DbConnection dbConnection,
            EmailService emailService)
        {
            _dbConnection = dbConnection;
            _emailService = emailService;
            _passwordHasher = new PasswordHasher<Usuario>();
        }

        // =====================================================
        // FORGOT PASSWORD - GET
        // =====================================================

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View("~/Views/Home/ForgotPassword.cshtml");
        }

        // =====================================================
        // FORGOT PASSWORD - POST
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(
            ForgotPasswordViewModel modelo)
        {
            if (!ModelState.IsValid)
            {
                return View(
                    "~/Views/Home/ForgotPassword.cshtml",
                    modelo);
            }

            try
            {
                using var connection =
                    _dbConnection.CreateConnection();

                modelo.Email = modelo.Email.Trim().ToLower();

                var parametros = new DynamicParameters();

                parametros.Add(
                    "p_email",
                    modelo.Email,
                    DbType.String,
                    ParameterDirection.Input);

                parametros.Add(
                    "o_id_usuario",
                    dbType: DbType.Int32,
                    direction: ParameterDirection.Output);

                parametros.Add(
                    "o_nombre",
                    dbType: DbType.String,
                    direction: ParameterDirection.Output);

                parametros.Add(
                    "o_usuario",
                    dbType: DbType.String,
                    direction: ParameterDirection.Output);

                parametros.Add(
                    "o_contrasena_hash",
                    dbType: DbType.String,
                    direction: ParameterDirection.Output);

                parametros.Add(
                    "o_email",
                    dbType: DbType.String,
                    direction: ParameterDirection.Output);

                parametros.Add(
                    "o_rol_id",
                    dbType: DbType.Int32,
                    direction: ParameterDirection.Output);

                parametros.Add(
                    "o_estado",
                    dbType: DbType.Boolean,
                    direction: ParameterDirection.Output);

                parametros.Add(
                    "o_fecha_creacion",
                    dbType: DbType.DateTime,
                    direction: ParameterDirection.Output);

                connection.Execute(
                    "sp_obtener_usuario_por_email",
                    parametros,
                    commandType: CommandType.StoredProcedure);

                int? idUsuario =
                    parametros.Get<int?>("o_id_usuario");

                if (!idUsuario.HasValue)
                {
                    ModelState.AddModelError(
                        "Email",
                        "No se encontró una cuenta activa asociada a este correo electrónico.");

                    return View(
                        "~/Views/Home/ForgotPassword.cshtml",
                        modelo);
                }

                var usuario = new Usuario
                {
                    IdUsuario = idUsuario.Value,
                    Nombre = parametros.Get<string?>("o_nombre") ?? string.Empty,
                    UsuarioNombre = parametros.Get<string?>("o_usuario") ?? string.Empty,
                    ContrasenaHash = parametros.Get<string?>("o_contrasena_hash") ?? string.Empty,
                    Email = parametros.Get<string?>("o_email"),
                    RolId = parametros.Get<int?>("o_rol_id") ?? 0,
                    Estado = parametros.Get<bool?>("o_estado") ?? false,
                    FechaCreacion = parametros.Get<DateTime?>("o_fecha_creacion") ?? DateTime.MinValue
                };

                string contrasenaTemporal =
                    GenerarContrasenaTemporal();

                string nuevaContrasenaHash =
                    _passwordHasher.HashPassword(
                        usuario,
                        contrasenaTemporal);

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
                    "sp_actualizar_contrasena",
                    parametrosActualizar,
                    commandType: CommandType.StoredProcedure);

                string nombreUsuario =
                    string.IsNullOrWhiteSpace(usuario.Nombre)
                        ? usuario.UsuarioNombre
                        : usuario.Nombre;

                string cuerpoCorreo = $"""
                    <html>
                    <body style="font-family: Arial, sans-serif; color: #333333;">

                        <h2 style="color: #171717;">
                            Charlie Shop
                        </h2>

                        <p>
                            Hola <strong>{nombreUsuario}</strong>,
                        </p>

                        <p>
                            Se ha generado una contraseña temporal
                            para tu cuenta de Charlie Shop.
                        </p>

                        <p>
                            Tu contraseña temporal es:
                        </p>

                        <p style="
                            font-size: 20px;
                            font-weight: bold;
                            letter-spacing: 2px;
                            background: #f4f4f4;
                            padding: 12px 16px;
                            display: inline-block;
                            border-radius: 6px;">
                            {contrasenaTemporal}
                        </p>

                        <p>
                            Utiliza esta contraseña para iniciar sesión
                            y luego cámbiala desde tu perfil.
                        </p>

                        <p>
                            Si no solicitaste recuperar tu contraseña,
                            puedes ignorar este correo.
                        </p>

                        <br />

                        <p>
                            Saludos,<br />
                            <strong>Charlie Shop</strong>
                        </p>

                    </body>
                    </html>
                    """;

                await _emailService.SendAsync(
                    usuario.Email!,
                    "Charlie Shop - Contraseña temporal",
                    cuerpoCorreo);

                TempData["Mensaje"] =
                    "Se envió una contraseña temporal a tu correo electrónico.";

                return RedirectToAction(
                    nameof(ForgotPassword));
            }
            catch (PostgresException)
            {
                ModelState.AddModelError(
                    "",
                    "No fue posible procesar la solicitud. Intente nuevamente.");

                return View(
                    "~/Views/Home/ForgotPassword.cshtml",
                    modelo);
            }
            catch (NpgsqlException)
            {
                ModelState.AddModelError(
                    "",
                    "No fue posible conectar con la base de datos. Intente nuevamente.");

                return View(
                    "~/Views/Home/ForgotPassword.cshtml",
                    modelo);
            }
            catch (Exception)
            {
                ModelState.AddModelError(
                    "",
                    "No fue posible enviar el correo. Verifique la configuración del servicio de correo.");

                return View(
                    "~/Views/Home/ForgotPassword.cshtml",
                    modelo);
            }
        }

        // =====================================================
        // GENERAR CONTRASEÑA TEMPORAL
        // =====================================================

        private static string GenerarContrasenaTemporal()
        {
            const string caracteres =
                "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";

            char[] resultado = new char[8];

            for (int i = 0; i < resultado.Length; i++)
            {
                int indice =
                    RandomNumberGenerator.GetInt32(
                        caracteres.Length);

                resultado[i] = caracteres[indice];
            }

            return new string(resultado);
        }

        // =====================================================
        // LOGIN - GET
        // =====================================================

        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction(
                    "HomePage",
                    "Home");
            }

            return View(
                "~/Views/Home/Index.cshtml");
        }

        // =====================================================
        // LOGIN - POST
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            LoginViewModel modelo)
        {
            if (!ModelState.IsValid)
            {
                return View(
                    "~/Views/Home/Index.cshtml",
                    modelo);
            }

            modelo.Usuario =
                modelo.Usuario.Trim();

            try
            {
                using var connection =
                    _dbConnection.CreateConnection();

                var parametros = new DynamicParameters();

                parametros.Add(
                    "p_usuario",
                    modelo.Usuario,
                    DbType.String,
                    ParameterDirection.Input);

                parametros.Add(
                    "o_id_usuario",
                    dbType: DbType.Int32,
                    direction: ParameterDirection.Output);

                parametros.Add(
                    "o_nombre",
                    dbType: DbType.String,
                    direction: ParameterDirection.Output);

                parametros.Add(
                    "o_usuario_nombre",
                    dbType: DbType.String,
                    direction: ParameterDirection.Output);

                parametros.Add(
                    "o_contrasena_hash",
                    dbType: DbType.String,
                    direction: ParameterDirection.Output);

                parametros.Add(
                    "o_email",
                    dbType: DbType.String,
                    direction: ParameterDirection.Output);

                parametros.Add(
                    "o_rol_id",
                    dbType: DbType.Int32,
                    direction: ParameterDirection.Output);

                parametros.Add(
                    "o_estado",
                    dbType: DbType.Boolean,
                    direction: ParameterDirection.Output);

                parametros.Add(
                    "o_fecha_creacion",
                    dbType: DbType.DateTime,
                    direction: ParameterDirection.Output);

                parametros.Add(
                    "o_nombre_rol",
                    dbType: DbType.String,
                    direction: ParameterDirection.Output);

                connection.Execute(
                    "sp_obtener_usuario_login",
                    parametros,
                    commandType: CommandType.StoredProcedure);

                int? idUsuario =
                    parametros.Get<int?>("o_id_usuario");

                if (!idUsuario.HasValue)
                {
                    ModelState.AddModelError(
                        "",
                        "El usuario o la contraseña son incorrectos.");

                    return View(
                        "~/Views/Home/Index.cshtml",
                        modelo);
                }

                var usuario = new UsuarioLogin
                {
                    IdUsuario = idUsuario.Value,
                    Nombre = parametros.Get<string?>("o_nombre") ?? string.Empty,
                    UsuarioNombre = parametros.Get<string?>("o_usuario_nombre") ?? string.Empty,
                    ContrasenaHash = parametros.Get<string?>("o_contrasena_hash") ?? string.Empty,
                    Email = parametros.Get<string?>("o_email"),
                    RolId = parametros.Get<int?>("o_rol_id") ?? 0,
                    Estado = parametros.Get<bool?>("o_estado") ?? false,
                    FechaCreacion = parametros.Get<DateTime?>("o_fecha_creacion") ?? DateTime.MinValue,
                    NombreRol = parametros.Get<string?>("o_nombre_rol") ?? string.Empty
                };

                if (!usuario.Estado)
                {
                    ModelState.AddModelError(
                        "",
                        "Esta cuenta se encuentra desactivada.");

                    return View(
                        "~/Views/Home/Index.cshtml",
                        modelo);
                }

                var usuarioParaHash = new Usuario
                {
                    IdUsuario = usuario.IdUsuario,
                    Nombre = usuario.Nombre,
                    UsuarioNombre = usuario.UsuarioNombre,
                    ContrasenaHash = usuario.ContrasenaHash,
                    Email = usuario.Email,
                    RolId = usuario.RolId,
                    Estado = usuario.Estado,
                    FechaCreacion = usuario.FechaCreacion
                };

                var resultado =
                    _passwordHasher.VerifyHashedPassword(
                        usuarioParaHash,
                        usuario.ContrasenaHash,
                        modelo.Contrasena);

                if (resultado ==
                    PasswordVerificationResult.Failed)
                {
                    ModelState.AddModelError(
                        "",
                        "El usuario o la contraseña son incorrectos.");

                    return View(
                        "~/Views/Home/Index.cshtml",
                        modelo);
                }

                var claims = new List<Claim>
                {
                    new Claim(
                        ClaimTypes.NameIdentifier,
                        usuario.IdUsuario.ToString()),

                    new Claim(
                        ClaimTypes.Name,
                        usuario.UsuarioNombre),

                    new Claim(
                        "NombreCompleto",
                        usuario.Nombre),

                    new Claim(
                        ClaimTypes.Role,
                        usuario.NombreRol)
                };

                if (!string.IsNullOrWhiteSpace(usuario.Email))
                {
                    claims.Add(
                        new Claim(
                            ClaimTypes.Email,
                            usuario.Email));
                }

                var identity =
                    new ClaimsIdentity(
                        claims,
                        CookieAuthenticationDefaults.AuthenticationScheme);

                var principal =
                    new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    principal,
                    new AuthenticationProperties
                    {
                        IsPersistent = false,
                        AllowRefresh = true
                    });

                return RedirectToAction(
                    "HomePage",
                    "Home");
            }
            catch (PostgresException)
            {
                ModelState.AddModelError(
                    "",
                    "No fue posible procesar la solicitud. Intente nuevamente.");

                return View(
                    "~/Views/Home/Index.cshtml",
                    modelo);
            }
            catch (NpgsqlException)
            {
                ModelState.AddModelError(
                    "",
                    "No fue posible conectar con la base de datos. Intente nuevamente.");

                return View(
                    "~/Views/Home/Index.cshtml",
                    modelo);
            }
            catch (Exception)
            {
                ModelState.AddModelError(
                    "",
                    "No fue posible iniciar sesión. Intente nuevamente.");

                return View(
                    "~/Views/Home/Index.cshtml",
                    modelo);
            }
        }

        // =====================================================
        // LOGOUT
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

            return RedirectToAction(
                "Login",
                "Account");
        }

        // =====================================================
        // ACCESS DENIED
        // =====================================================

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }

    // =========================================================
    // MODELO INTERNO PARA EL LOGIN
    // =========================================================

    public class UsuarioLogin
    {
        public int IdUsuario { get; set; }

        public string Nombre { get; set; } = string.Empty;

        public string UsuarioNombre { get; set; } = string.Empty;

        public string ContrasenaHash { get; set; } = string.Empty;

        public string? Email { get; set; }

        public int RolId { get; set; }

        public bool Estado { get; set; }

        public DateTime FechaCreacion { get; set; }

        public string NombreRol { get; set; } = string.Empty;
    }
}
