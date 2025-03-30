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
        [Required(ErrorMessage = "La nueva contrase�a es requerida")]
        [StringLength(100, ErrorMessage = "La contrase�a debe tener al menos {2} caracteres de longitud.", MinimumLength = 8)]
        [DataType(DataType.Password)]
        public required string NewPassword { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "La confirmaci�n de contrase�a es requerida")]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "Las contrase�as no coinciden")]
        public required string ConfirmPassword { get; set; }

        [BindProperty(SupportsGet = true)]
        public required string Token { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public required string Email { get; set; } = string.Empty;

        public string? ErrorMessage { get; set; }

        public IActionResult OnGet()
        {
            // Verificar que el token y el email est�n presentes
            if (string.IsNullOrEmpty(Token) || string.IsNullOrEmpty(Email))
            {
                ErrorMessage = "Enlace de restablecimiento de contrase�a inv�lido o expirado.";
                return Page();
            }

            // Verificar si el token es v�lido
            if (!ValidatePasswordResetToken(Email, Token))
            {
                ErrorMessage = "El enlace de restablecimiento de contrase�a ha expirado o no es v�lido.";
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

                // Verificar que el token y el email est�n presentes
                if (string.IsNullOrEmpty(Token) || string.IsNullOrEmpty(Email))
                {
                    ErrorMessage = "Enlace de restablecimiento de contrase�a inv�lido o expirado.";
                    return Page();
                }

                // Validar el token nuevamente
                if (!ValidatePasswordResetToken(Email, Token))
                {
                    ErrorMessage = "El enlace de restablecimiento de contrase�a ha expirado o no es v�lido.";
                    return Page();
                }

                // Validar la fortaleza de la contrase�a
                if (!IsPasswordStrong(NewPassword))
                {
                    ErrorMessage = "La contrase�a debe contener al menos una letra may�scula, una min�scula, un n�mero y un car�cter especial.";
                    return Page();
                }

                // Cambiar la contrase�a directamente en la base de datos
                bool result = ResetPassword(Email, NewPassword, Token);

                if (result)
                {
                    // Registrar el cambio de contrase�a en el historial
                    LogPasswordChange(Email);

                    // Enviar correo de notificaci�n de cambio de contrase�a
                    await _emailService.SendPasswordChangeNotificationAsync(Email);

                    // Mensaje de �xito
                    TempData["SuccessMessage"] = "Tu contrase�a ha sido cambiada exitosamente. Ahora puedes iniciar sesi�n con tu nueva contrase�a.";
                    return RedirectToPage("/Cliente/Login");
                }
                else
                {
                    ErrorMessage = "No se pudo cambiar la contrase�a. Por favor, intenta nuevamente.";
                    return Page();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al restablecer la contrase�a");
                ErrorMessage = "Ocurri� un error al procesar tu solicitud. Por favor, intenta nuevamente.";
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

                        // Verificar si el token es v�lido y no ha expirado
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
                _logger.LogError(ex, "Error al validar el token de restablecimiento de contrase�a");
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

                            // Actualizar la contrase�a directamente (sin hash)
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
                _logger.LogError(ex, "Error al restablecer la contrase�a");
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
                            // Registrar el cambio de contrase�a
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
                _logger.LogError(ex, "Error al registrar el cambio de contrase�a");
            }
        }

        private bool IsPasswordStrong(string password)
        {
            // Verificar que la contrase�a tenga al menos:
            // - Una letra may�scula
            // - Una letra min�scula
            // - Un n�mero
            // - Un car�cter especial
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