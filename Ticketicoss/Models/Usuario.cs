namespace WebApplication1.Models
{
    public class Usuario
    {
        public int IdUsuario { get; set; } // Identificador único del usuario
        public string Correo { get; set; } = string.Empty; // Correo electrónico del usuario
        public string Contrasena { get; set; } = string.Empty; // Contraseña del usuario
        public string Nombre { get; set; } = string.Empty; // Nombre del usuario
        public string Apellido { get; set; } = string.Empty; // Apellido del usuario
        public DateTime FechaRegistro { get; set; } // Fecha en que se registró el usuario
    }
}

