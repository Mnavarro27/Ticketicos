using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace WebApplication1.Pages.Cliente
{
    public class RegistroModel : PageModel
    {
        private readonly ILogger<RegistroModel> _logger;
        private readonly string _connectionString;

        public RegistroModel(ILogger<RegistroModel> logger, IConfiguration configuration)
        {
            _logger = logger;

            // Get the connection string from configuration with null checking
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found in configuration.");

            Usuario = new UsuarioInputModel();
        }

        [BindProperty]
        public UsuarioInputModel Usuario { get; set; }

        [BindProperty]
        [DataType(DataType.Password)]
        [Display(Name = "Confirmar contraseña")]
        public string ConfirmarContrasena { get; set; } = string.Empty;

        [TempData]
        public string? ErrorMessage { get; set; }

        public bool RegistroExitoso { get; set; } = false;

        public class UsuarioInputModel
        {
            [Required(ErrorMessage = "El nombre es requerido")]
            public string Nombre { get; set; } = string.Empty;

            [Required(ErrorMessage = "Los apellidos son requeridos")]
            public string Apellidos { get; set; } = string.Empty;

            [Required(ErrorMessage = "El correo electrónico es requerido")]
            [EmailAddress(ErrorMessage = "El formato del correo electrónico no es válido")]
            public string Correo { get; set; } = string.Empty;

            [Required(ErrorMessage = "La contraseña es requerida")]
            [StringLength(100, ErrorMessage = "La {0} debe tener al menos {2} caracteres de longitud.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Contraseña")]
            public string Contrasena { get; set; } = string.Empty;

            [Required(ErrorMessage = "La identificación es requerida")]
            public string Identificacion { get; set; } = string.Empty;
        }

        public void OnGet()
        {
            // Página de registro
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Validación manual para contraseñas
            if (ConfirmarContrasena != Usuario.Contrasena)
            {
                ModelState.AddModelError("ConfirmarContrasena", "Las contraseñas no coinciden.");
                return Page();
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                // Verificar si el correo ya existe
                if (await CorreoExisteAsync(Usuario.Correo))
                {
                    ErrorMessage = "El correo electrónico ya está registrado.";
                    return Page();
                }

                // Verificar si la identificación ya existe
                if (await IdentificacionExisteAsync(Usuario.Identificacion))
                {
                    ErrorMessage = "La identificación ya está registrada.";
                    return Page();
                }

                // Registrar el usuario
                await RegistrarUsuarioAsync();

                RegistroExitoso = true;
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar usuario");
                ErrorMessage = $"Error al registrar: {ex.Message}";
                return Page();
            }
        }

        private async Task<bool> CorreoExisteAsync(string correo)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand("SELECT COUNT(1) FROM Usuarios WHERE correo = @correo", connection))
                {
                    command.Parameters.AddWithValue("@correo", correo);
                    var result = await command.ExecuteScalarAsync();
                    return result != null && Convert.ToInt32(result) > 0;
                }
            }
        }

        private async Task<bool> IdentificacionExisteAsync(string identificacion)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand("SELECT COUNT(1) FROM Usuarios WHERE identificacion = @identificacion", connection))
                {
                    command.Parameters.AddWithValue("@identificacion", identificacion);
                    var result = await command.ExecuteScalarAsync();
                    return result != null && Convert.ToInt32(result) > 0;
                }
            }
        }

        private async Task RegistrarUsuarioAsync()
        {
            // Hashear la contraseña
            string hashedPassword = HashPassword(Usuario.Contrasena);

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand(@"
                    INSERT INTO Usuarios (nombre, apellidos, correo, contrasena, identificacion, tipo_usuario, estado, fecha_registro)
                    VALUES (@nombre, @apellidos, @correo, @contrasena, @identificacion, 'cliente', 'activo', @fechaRegistro)", connection))
                {
                    command.Parameters.AddWithValue("@nombre", Usuario.Nombre);
                    command.Parameters.AddWithValue("@apellidos", Usuario.Apellidos);
                    command.Parameters.AddWithValue("@correo", Usuario.Correo);
                    command.Parameters.AddWithValue("@contrasena", hashedPassword);
                    command.Parameters.AddWithValue("@identificacion", Usuario.Identificacion);
                    command.Parameters.AddWithValue("@fechaRegistro", DateTime.Now);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        // Método para hashear contraseñas
        private string HashPassword(string password)
        {
            // Usamos RandomNumberGenerator en lugar de RNGCryptoServiceProvider
            byte[] salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            byte[] hash = new Rfc2898DeriveBytes(
                password, salt, 10000, HashAlgorithmName.SHA256).GetBytes(32);

            byte[] hashBytes = new byte[48];
            Array.Copy(salt, 0, hashBytes, 0, 16);
            Array.Copy(hash, 0, hashBytes, 16, 32);

            return Convert.ToBase64String(hashBytes);
        }
    }
}

