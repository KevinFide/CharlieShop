using CharlieShop.Data;
using CharlieShop.Models;
using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CharlieShop.Controllers
{
    public class AccountController : Controller
    {
        private readonly DbConnection _dbConnection;
        private readonly PasswordHasher<Usuario> _passwordHasher;

        public AccountController(DbConnection dbConnection)
        {
            _dbConnection = dbConnection;
            _passwordHasher = new PasswordHasher<Usuario>();
        }


        // =====================================================
        // LOGIN - GET
        // =====================================================

        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("HomePage", "Home");
            }

            return View("~/Views/Home/Index.cshtml");
        }


        // =====================================================
        // LOGIN - POST
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel modelo)
        {
            if (!ModelState.IsValid)
            {
                return View("~/Views/Home/Index.cshtml", modelo);
            }

            modelo.Usuario = modelo.Usuario.Trim();

            try
            {
                using var connection = _dbConnection.CreateConnection();

                const string consulta = """
                    SELECT
                        u.id_usuario AS IdUsuario,
                        u.nombre AS Nombre,
                        u.usuario AS UsuarioNombre,
                        u.contrasena_hash AS ContrasenaHash,
                        u.email AS Email,
                        u.rol_id AS RolId,
                        u.estado AS Estado,
                        u.fecha_creacion AS FechaCreacion,
                        r.nombre AS NombreRol
                    FROM usuario u
                    INNER JOIN rol r
                        ON r.id_rol = u.rol_id
                    WHERE u.usuario = @Usuario
                    LIMIT 1
                """;

                var usuario = connection.QuerySingleOrDefault<UsuarioLogin>(
                    consulta,
                    new
                    {
                        Usuario = modelo.Usuario
                    });


                // =================================================
                // USUARIO NO EXISTE
                // =================================================

                if (usuario == null)
                {
                    ModelState.AddModelError(
                        "",
                        "El usuario o la contraseña son incorrectos.");

                    return View("~/Views/Home/Index.cshtml", modelo);
                }


                // =================================================
                // CUENTA DESACTIVADA
                // =================================================

                if (!usuario.Estado)
                {
                    ModelState.AddModelError(
                        "",
                        "Esta cuenta se encuentra desactivada.");

                    return View("~/Views/Home/Index.cshtml", modelo);
                }


                // =================================================
                // VERIFICAR CONTRASEÑA
                // =================================================

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

                var resultado = _passwordHasher.VerifyHashedPassword(
                    usuarioParaHash,
                    usuario.ContrasenaHash,
                    modelo.Contrasena);


                if (resultado == PasswordVerificationResult.Failed)
                {
                    ModelState.AddModelError(
                        "",
                        "El usuario o la contraseña son incorrectos.");

                    return View("~/Views/Home/Index.cshtml", modelo);
                }


                // =================================================
                // CREAR CLAIMS
                // =================================================

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


                var identity = new ClaimsIdentity(
                    claims,
                    CookieAuthenticationDefaults.AuthenticationScheme);


                var principal = new ClaimsPrincipal(identity);


                // =================================================
                // CREAR COOKIE
                // =================================================

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    principal,
                    new AuthenticationProperties
                    {
                        IsPersistent = false,
                        AllowRefresh = true
                    });


                // =================================================
                // REDIRECT HOME
                // =================================================

                return RedirectToAction(
                    "HomePage",
                    "Home");
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