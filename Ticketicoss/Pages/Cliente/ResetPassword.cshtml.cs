using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using WebApplication1.Services;
using Microsoft.Data.SqlClient; // Updated to Microsoft.Data.SqlClient
using Microsoft.Extensions.Configuration;

namespace WebApplication1.Pages.Cliente
{
    public class ResetPasswordModel : PageModel
    {
        private readonly ILogger<ResetPasswordModel> _logger;
        private readonly IConfiguration _configuration;
        private readonly EmailService _emailService;
        private readonly string _connectionString;

        public ResetPasswordModel(
            ILogger<ResetPasswordModel> logger,
            IConfiguration configuration,
            EmailService emailService)
        {
            _logger = logger;
            _configuration = configuration;
            _emailService = emailService;
            _connectionString = _configuration.GetConnectionString("DefaultConnection") ??
                throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        }

        [BindProperty]
        [Required(ErrorMessage = "La nueva contraseña es requerida")]
        [StringLength(100, ErrorMessage = "La contraseña debe tener al menos {2} caracteres de longitud.", MinimumLength = 8)]
        [DataType(DataType.Password)]
        public required string NewPassword { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "La confirmación de contraseña es requerida")]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "Las contraseñas no coinciden")]
        public required string ConfirmPassword { get; set; }

        [BindProperty(SupportsGet = true)]
        public required string Token { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public required string Email { get; set; } = string.Empty;

        public string? ErrorMessage { get; set; }

        public IActionResult OnGet()
        {
            // Verificar que el token y el email estén presentes
            if (string.IsNullOrEmpty(Token) || string.IsNullOrEmpty(Email))
            {
                ErrorMessage = "Enlace de restablecimiento de contraseña inválido o expirado.";
                return Page();
            }

            // Verificar si el token es válido
            if (!ValidatePasswordResetToken(Email, Token))
            {
                ErrorMessage = "El enlace de restablecimiento de contraseña ha expirado o no es válido.";
                return Page();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return Page();
                }

                // Verificar que el token y el email estén presentes
                if (string.IsNullOrEmpty(Token) || string.IsNullOrEmpty(Email))
                {
                    ErrorMessage = "Enlace de restablecimiento de contraseña inválido o expirado.";
                    return Page();
                }

                // Validar el token nuevamente
                if (!ValidatePasswordResetToken(Email, Token))
                {
                    ErrorMessage = "El enlace de restablecimiento de contraseña ha expirado o no es válido.";
                    return Page();
                }

                // Validar la fortaleza de la contraseña
                if (!IsPasswordStrong(NewPassword))
                {
                    ErrorMessage = "La contraseña debe contener al menos una letra mayúscula, una minúscula, un número y un carácter especial.";
                    return Page();
                }

                // Cambiar la contraseña directamente en la base de datos
                bool result = ResetPassword(Email, NewPassword, Token);

                if (result)
                {
                    // Registrar el cambio de contraseña en el historial
                    LogPasswordChange(Email);

                    // Enviar correo de notificación de cambio de contraseña
                    await _emailService.SendPasswordChangeNotificationAsync(Email);

                    // Mensaje de éxito
                    TempData["SuccessMessage"] = "Tu contraseña ha sido cambiada exitosamente. Ahora puedes iniciar sesión con tu nueva contraseña.";
                    return RedirectToPage("/Cliente/Login");
                }
                else
                {
                    ErrorMessage = "No se pudo cambiar la contraseña. Por favor, intenta nuevamente.";
                    return Page();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al restablecer la contraseña");
                ErrorMessage = "Ocurrió un error al procesar tu solicitud. Por favor, intenta nuevamente.";
                return Page();
            }
        }

        private bool ValidatePasswordResetToken(string email, string token)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();

                    // Obtener el ID del usuario
                    using (var command = new SqlCommand("SELECT id_usuario FROM Usuarios WHERE correo = @Correo", connection))
                    {
                        command.Parameters.AddWithValue("@Correo", email);
                        var userId = command.ExecuteScalar();

                        if (userId == null || userId == DBNull.Value)
                            return false;

                        // Verificar si el token es válido y no ha expirado
                        using (var tokenCommand = new SqlCommand(
                            @"SELECT COUNT(1) FROM Autenticacion 
                              WHERE id_usuario = @IdUsuario 
                              AND codigo_recuperacion = @Token 
                              AND fecha_expiracion > GETDATE() 
                              AND utilizado = 0", connection))
                        {
                            tokenCommand.Parameters.AddWithValue("@IdUsuario", userId);
                            tokenCommand.Parameters.AddWithValue("@Token", token);

                            int count = (int)tokenCommand.ExecuteScalar();
                            return count > 0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al validar el token de restablecimiento de contraseña");
                return false;
            }
        }

        private bool ResetPassword(string email, string newPassword, string token)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();

                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // Obtener el ID del usuario
                            int userId;
                            using (var command = new SqlCommand("SELECT id_usuario FROM Usuarios WHERE correo = @Correo", connection, transaction))
                            {
                                command.Parameters.AddWithValue("@Correo", email);
                                var result = command.ExecuteScalar();

                                if (result == null || result == DBNull.Value)
                                    return false;

                                userId = (int)result;
                            }

                            // Actualizar la contraseña directamente (sin hash)
                            using (var updateCommand = new SqlCommand(
                                "UPDATE Usuarios SET contrasena = @Password WHERE id_usuario = @UserId",
                                connection, transaction))
                            {
                                updateCommand.Parameters.AddWithValue("@Password", newPassword);
                                updateCommand.Parameters.AddWithValue("@UserId", userId);

                                int rowsAffected = updateCommand.ExecuteNonQuery();
                                if (rowsAffected <= 0)
                                    return false;
                            }

                            // Marcar el token como utilizado
                            using (var tokenCommand = new SqlCommand(
                                "UPDATE Autenticacion SET utilizado = 1 WHERE id_usuario = @UserId AND codigo_recuperacion = @Token",
                                connection, transaction))
                            {
                                tokenCommand.Parameters.AddWithValue("@UserId", userId);
                                tokenCommand.Parameters.AddWithValue("@Token", token);

                                tokenCommand.ExecuteNonQuery();
                            }

                            transaction.Commit();
                            return true;
                        }
                        catch
                        {
                            transaction.Rollback();
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al restablecer la contraseña");
                return false;
            }
        }

        private void LogPasswordChange(string email)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();

                    // Obtener el ID del usuario
                    using (var command = new SqlCommand("SELECT id_usuario FROM Usuarios WHERE correo = @Correo", connection))
                    {
                        command.Parameters.AddWithValue("@Correo", email);
                        var userId = command.ExecuteScalar();

                        if (userId != null && userId != DBNull.Value)
                        {
                            // Registrar el cambio de contraseña
                            using (var insertCommand = new SqlCommand(
                                "INSERT INTO HistorialCambiosContrasena (id_usuario, fecha_cambio) VALUES (@UserId, GETDATE())",
                                connection))
                            {
                                insertCommand.Parameters.AddWithValue("@UserId", userId);
                                insertCommand.ExecuteNonQuery();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar el cambio de contraseña");
            }
        }

        private bool IsPasswordStrong(string password)
        {
            // Verificar que la contraseña tenga al menos:
            // - Una letra mayúscula
            // - Una letra minúscula
            // - Un número
            // - Un carácter especial
            bool hasUpperCase = false;
            bool hasLowerCase = false;
            bool hasDigit = false;
            bool hasSpecialChar = false;
            string specialChars = @"!@#$%^&*()_+-=[]{}|;':"",./<>?`~";

            foreach (char c in password)
            {
                if (char.IsUpper(c)) hasUpperCase = true;
                else if (char.IsLower(c)) hasLowerCase = true;
                else if (char.IsDigit(c)) hasDigit = true;
                else if (specialChars.Contains(c)) hasSpecialChar = true;
            }

            return hasUpperCase && hasLowerCase && hasDigit && hasSpecialChar;
        }
    }
}