using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;

namespace WebApplication1.Services
{
    public class AuthService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthService> _logger;
        private readonly EmailService _emailService;
        private readonly string _connectionString;

        public AuthService(IConfiguration configuration, ILogger<AuthService> logger, EmailService emailService)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));

            // Obtener la cadena de conexión y verificar que no sea nula
            _connectionString = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada.");
        }

        public async Task<(int? userId, bool requiresTwoFactor)> ValidateCredentialsAsync(string correo, string contrasena, string ip)
        {
            if (string.IsNullOrEmpty(correo))
                throw new ArgumentNullException(nameof(correo));

            if (string.IsNullOrEmpty(contrasena))
                throw new ArgumentNullException(nameof(contrasena));

            if (string.IsNullOrEmpty(ip))
                ip = "Unknown"; // Valor predeterminado si IP es nula

            try
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = "SELECT id_usuario, contrasena, doble_factor_habilitado, estado FROM Usuarios WHERE correo = @Correo";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Correo", correo);

                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                int userId = reader.GetInt32(0);
                                string hashedPassword = reader.GetString(1);
                                bool twoFactorEnabled = reader.GetBoolean(2);
                                string estado = reader.GetString(3);

                                if (estado != "activo")
                                {
                                    await RegisterAuthenticationAttemptAsync(userId, "password", false, ip);
                                    return (null, false);
                                }

                                if (VerifyPassword(contrasena, hashedPassword))
                                {
                                    await UpdateLastLoginAsync(userId);
                                    await RegisterAuthenticationAttemptAsync(userId, "password", true, ip);
                                    return (userId, twoFactorEnabled);
                                }
                                else
                                {
                                    await RegisterAuthenticationAttemptAsync(userId, "password", false, ip);
                                    return (null, false);
                                }
                            }
                        }
                    }
                }

                return (null, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al validar credenciales");
                return (null, false);
            }
        }

        public async Task<bool> GenerateAndSendTwoFactorCodeAsync(int userId)
        {
            try
            {
                string verificationCode = GenerateRandomCode(5);
                string? userEmail = await GetUserEmailAsync(userId);
                if (string.IsNullOrEmpty(userEmail))
                {
                    return false;
                }

                DateTime now = DateTime.Now;
                DateTime expiration = now.AddMinutes(10);

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string invalidateQuery = "UPDATE Autenticacion SET utilizado = 1 WHERE id_usuario = @UserId AND utilizado = 0";
                    using (SqlCommand invalidateCommand = new SqlCommand(invalidateQuery, connection))
                    {
                        invalidateCommand.Parameters.AddWithValue("@UserId", userId);
                        await invalidateCommand.ExecuteNonQueryAsync();
                    }

                    string insertQuery = @"
                        INSERT INTO Autenticacion (id_usuario, codigo_2FA, fecha_generacion, fecha_expiracion, utilizado)
                        VALUES (@UserId, @Code, @Generated, @Expiration, 0)";

                    using (SqlCommand insertCommand = new SqlCommand(insertQuery, connection))
                    {
                        insertCommand.Parameters.AddWithValue("@UserId", userId);
                        insertCommand.Parameters.AddWithValue("@Code", verificationCode);
                        insertCommand.Parameters.AddWithValue("@Generated", now);
                        insertCommand.Parameters.AddWithValue("@Expiration", expiration);

                        await insertCommand.ExecuteNonQueryAsync();
                    }
                }

                bool emailSent = await _emailService.SendVerificationCodeAsync(userEmail, verificationCode);
                return emailSent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar y enviar código de doble factor");
                return false;
            }
        }

        public async Task<bool> VerifyTwoFactorCodeAsync(int userId, string code, string ip)
        {
            if (string.IsNullOrEmpty(code))
                throw new ArgumentNullException(nameof(code));

            if (string.IsNullOrEmpty(ip))
                ip = "Unknown"; // Valor predeterminado si IP es nula

            try
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"
                        SELECT id FROM Autenticacion 
                        WHERE id_usuario = @UserId 
                        AND codigo_2FA = @Code 
                        AND utilizado = 0 
                        AND fecha_expiracion > @Now";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@UserId", userId);
                        command.Parameters.AddWithValue("@Code", code);
                        command.Parameters.AddWithValue("@Now", DateTime.Now);

                        object? result = await command.ExecuteScalarAsync();

                        if (result != null)
                        {
                            int authId = Convert.ToInt32(result);

                            string updateQuery = "UPDATE Autenticacion SET utilizado = 1 WHERE id = @AuthId";
                            using (SqlCommand updateCommand = new SqlCommand(updateQuery, connection))
                            {
                                updateCommand.Parameters.AddWithValue("@AuthId", authId);
                                await updateCommand.ExecuteNonQueryAsync();
                            }

                            await RegisterAuthenticationAttemptAsync(userId, "2FA", true, ip);

                            return true;
                        }
                        else
                        {
                            await RegisterAuthenticationAttemptAsync(userId, "2FA", false, ip);
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar código de doble factor");
                return false;
            }
        }

        private async Task RegisterAuthenticationAttemptAsync(int userId, string authType, bool success, string ip)
        {
            if (string.IsNullOrEmpty(authType))
                throw new ArgumentNullException(nameof(authType));

            try
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"
                        INSERT INTO RegistroAutenticacion (id_usuario, fecha_intento, tipo_autenticacion, exito, direccion_ip)
                        VALUES (@UserId, @Date, @AuthType, @Success, @IP)";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@UserId", userId);
                        command.Parameters.AddWithValue("@Date", DateTime.Now);
                        command.Parameters.AddWithValue("@AuthType", authType);
                        command.Parameters.AddWithValue("@Success", success);
                        command.Parameters.AddWithValue("@IP", ip);

                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar intento de autenticación");
            }
        }

        private async Task UpdateLastLoginAsync(int userId)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = "UPDATE Usuarios SET ultima_fecha_acceso = @Now WHERE id_usuario = @UserId";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Now", DateTime.Now);
                        command.Parameters.AddWithValue("@UserId", userId);

                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar última fecha de acceso");
            }
        }

        private async Task<string?> GetUserEmailAsync(int userId)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = "SELECT correo FROM Usuarios WHERE id_usuario = @UserId";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@UserId", userId);

                        object? result = await command.ExecuteScalarAsync();
                        return result?.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener correo electrónico del usuario");
                return null;
            }
        }

        // MÉTODO ACTUALIZADO para verificar contraseñas con PBKDF2 y sal
        private bool VerifyPassword(string password, string storedHash)
        {
            try
            {
                // Convertir el hash almacenado de base64 a bytes
                byte[] hashBytes = Convert.FromBase64String(storedHash);

                // Extraer la sal (primeros 16 bytes)
                byte[] salt = new byte[16];
                Array.Copy(hashBytes, 0, salt, 0, 16);

                // Calcular el hash con la sal extraída
                byte[] computedHash = new Rfc2898DeriveBytes(
                    password, salt, 10000, HashAlgorithmName.SHA256).GetBytes(32);

                // Comparar el hash calculado con el hash almacenado
                for (int i = 0; i < 32; i++)
                {
                    if (hashBytes[i + 16] != computedHash[i])
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar contraseña");
                return false;
            }
        }

        private string GenerateRandomCode(int length)
        {
            if (length <= 0)
                throw new ArgumentException("La longitud debe ser mayor que cero", nameof(length));

            Random random = new Random();
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < length; i++)
            {
                builder.Append(random.Next(0, 10));
            }
            return builder.ToString();
        }
    }
}

