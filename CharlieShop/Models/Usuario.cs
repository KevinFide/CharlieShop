namespace CharlieShop.Models
{
    public class Usuario
    {
        public int IdUsuario { get; set; }

        public string Nombre { get; set; } = string.Empty;

        public string UsuarioNombre { get; set; } = string.Empty;

        public string ContrasenaHash { get; set; } = string.Empty;

        public string? Email { get; set; }

        public int RolId { get; set; }

        public bool Estado { get; set; }

        public DateTime FechaCreacion { get; set; }
    }
}