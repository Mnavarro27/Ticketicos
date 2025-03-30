using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RestSharp;
using RestSharp.Authenticators;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace WebApplication1.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;
        private readonly string _mailgunDomain;
        private readonly string _mailgunApiKey;
        private readonly string _connectionString;

        // Almacenamiento temporal del código de verificación con tiempo de expiración
        private static readonly Dictionary<int, (string Code, DateTime Expiration)> VerificationCodes = new Dictionary<int, (string, DateTime)>();

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Obtener las credenciales de Mailgun desde la configuración
            _mailgunDomain = _configuration["Mailgun:Domain"] ?? throw new ArgumentNullException("Mailgun:Domain");
            _mailgunApiKey = _configuration["Mailgun:ApiKey"] ?? throw new ArgumentNullException("Mailgun:ApiKey");

            // Obtener la cadena de conexión
            _connectionString = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada.");
        }

        public async Task<bool> SendPasswordChangeNotificationAsync(string recipientEmail)
        {
            try
            {
                var options = new RestClientOptions($"https://api.mailgun.net/v3/{_mailgunDomain}")
                {
                    Authenticator = new HttpBasicAuthenticator("api", _mailgunApiKey)
                };
                var client = new RestClient(options);
                var request = new RestRequest("messages");
                request.AddParameter("from", $"Soporte <mailgun@{_mailgunDomain}>");
                request.AddParameter("to", recipientEmail);
                request.AddParameter("subject", "Notificación de Cambio de Contraseña");
                request.AddParameter("text", "Su contraseña ha sido cambiada exitosamente.");
                request.AddParameter("html", "<h1>Su contraseña ha sido cambiada exitosamente.</h1>");

                var response = await client.PostAsync(request);

                if (response.IsSuccessful)
                {
                    _logger.LogInformation($"Notificación de cambio de contraseña enviada a {recipientEmail}");
                    return true;
                }
                else
                {
                    _logger.LogError($"Error al enviar notificación de cambio de contraseña: {response.ErrorMessage}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Excepción al enviar notificación de cambio de contraseña a {recipientEmail}");
                return false;
            }
        }

        public async Task<bool> SendVerificationCodeAsync(string recipientEmail, string verificationCode)
        {
            try
            {
                var options = new RestClientOptions($"https://api.mailgun.net/v3/{_mailgunDomain}")
                {
                    Authenticator = new HttpBasicAuthenticator("api", _mailgunApiKey)
                };
                var client = new RestClient(options);
                var request = new RestRequest("messages");
                request.AddParameter("from", $"Verificación <mailgun@{_mailgunDomain}>");
                request.AddParameter("to", recipientEmail);
                request.AddParameter("subject", "Código de Verificación");
                request.AddParameter("text", $"Su código de verificación es: {verificationCode}");
                request.AddParameter("html", $"<h1>Su código de verificación es: <strong>{verificationCode}</strong></h1>");

                var response = await client.PostAsync(request);

                if (response.IsSuccessful)
                {
                    _logger.LogInformation($"Código de verificación enviado a {recipientEmail}");
                    return true;
                }
                else
                {
                    _logger.LogError($"Error al enviar código de verificación: {response.ErrorMessage}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Excepción al enviar código de verificación a {recipientEmail}");
                return false;
            }
        }

        // Mantener los métodos originales
        public async Task<bool> SendPasswordResetEmailAsync(string recipientEmail, string resetLink)
        {
            try
            {
                var options = new RestClientOptions($"https://api.mailgun.net/v3/{_mailgunDomain}")
                {
                    Authenticator = new HttpBasicAuthenticator("api", _mailgunApiKey)
                };
                var client = new RestClient(options);
                var request = new RestRequest("messages");
                request.AddParameter("from", $"Soporte <mailgun@{_mailgunDomain}>");
                request.AddParameter("to", recipientEmail);
                request.AddParameter("subject", "Recuperación de Contraseña");
                request.AddParameter("text", $"Haz clic en el siguiente enlace para recuperar tu contraseña: {resetLink}");
                request.AddParameter("html", $"<h1>Recuperación de Contraseña</h1><p>Haz clic en el siguiente enlace para recuperar tu contraseña: <a href=\"{resetLink}\">{resetLink}</a></p>");

                var response = await client.PostAsync(request);

                if (response.IsSuccessful)
                {
                    _logger.LogInformation($"Correo de recuperación de contraseña enviado a {recipientEmail}");
                    return true;
                }
                else
                {
                    _logger.LogError($"Error al enviar correo de recuperación de contraseña: {response.ErrorMessage}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Excepción al enviar correo de recuperación de contraseña a {recipientEmail}");
                return false;
            }
        }

        public async Task<bool> SendInvoiceAsync(string recipientEmail, string invoiceNumber, string invoiceHtml)
        {
            try
            {
                var options = new RestClientOptions($"https://api.mailgun.net/v3/{_mailgunDomain}")
                {
                    Authenticator = new HttpBasicAuthenticator("api", _mailgunApiKey)
                };
                var client = new RestClient(options);
                var request = new RestRequest("messages");
                request.AddParameter("from", $"Facturación <mailgun@{_mailgunDomain}>");
                request.AddParameter("to", recipientEmail);
                request.AddParameter("subject", $"Factura #{invoiceNumber} - Tickicos");
                request.AddParameter("text", $"Adjuntamos su factura #{invoiceNumber}. Gracias por su compra.");
                request.AddParameter("html", invoiceHtml);

                var response = await client.PostAsync(request);

                if (response.IsSuccessful)
                {
                    _logger.LogInformation($"Factura enviada a {recipientEmail}");
                    return true;
                }
                else
                {
                    _logger.LogError($"Error al enviar factura: {response.ErrorMessage}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Excepción al enviar factura a {recipientEmail}");
                return false;
            }
        }

        public async Task<bool> SendTicketsAsync(string recipientEmail, string eventName, string ticketsHtml)
        {
            try
            {
                var options = new RestClientOptions($"https://api.mailgun.net/v3/{_mailgunDomain}")
                {
                    Authenticator = new HttpBasicAuthenticator("api", _mailgunApiKey)
                };
                var client = new RestClient(options);
                var request = new RestRequest("messages");
                request.AddParameter("from", $"Entradas <mailgun@{_mailgunDomain}>");
                request.AddParameter("to", recipientEmail);
                request.AddParameter("subject", $"Tus entradas para {eventName} - Tickicos");
                request.AddParameter("text", $"Adjuntamos tus entradas para {eventName}. ¡Disfruta del evento!");
                request.AddParameter("html", ticketsHtml);

                var response = await client.PostAsync(request);

                if (response.IsSuccessful)
                {
                    _logger.LogInformation($"Entradas enviadas a {recipientEmail}");
                    return true;
                }
                else
                {
                    _logger.LogError($"Error al enviar entradas: {response.ErrorMessage}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Excepción al enviar entradas a {recipientEmail}");
                return false;
            }
        }

        public async Task<bool> SendPromoCodeAsync(string recipientEmail, string promoCode)
        {
            try
            {
                var options = new RestClientOptions($"https://api.mailgun.net/v3/{_mailgunDomain}")
                {
                    Authenticator = new HttpBasicAuthenticator("api", _mailgunApiKey)
                };
                var client = new RestClient(options);
                var request = new RestRequest("messages");
                request.AddParameter("from", $"Promociones <mailgun@{_mailgunDomain}>");
                request.AddParameter("to", recipientEmail);
                request.AddParameter("subject", "Código Promocional");
                request.AddParameter("text", $"Tu código promocional es: {promoCode}");
                request.AddParameter("html", $"<h1>Tu código promocional es: <strong>{promoCode}</strong></h1>");

                var response = await client.PostAsync(request);

                if (response.IsSuccessful)
                {
                    _logger.LogInformation($"Código promocional enviado a {recipientEmail}");
                    return true;
                }
                else
                {
                    _logger.LogError($"Error al enviar código promocional: {response.ErrorMessage}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Excepción al enviar código promocional a {recipientEmail}");
                return false;
            }
        }

        // Método mejorado para generar y enviar código de doble factor
        public async Task<bool> GenerateAndSendTwoFactorCodeAsync(int userId)
        {
            try
            {
                string verificationCode = GenerateVerificationCode();

                // Establecer tiempo de expiración (10 minutos)
                DateTime expiration = DateTime.Now.AddMinutes(10);

                // Guardar el código y su tiempo de expiración
                VerificationCodes[userId] = (verificationCode, expiration);

                _logger.LogInformation($"Nuevo código generado para usuario {userId}: {verificationCode}, expira: {expiration}");

                var options = new RestClientOptions($"https://api.mailgun.net/v3/{_mailgunDomain}")
                {
                    Authenticator = new HttpBasicAuthenticator("api", _mailgunApiKey)
                };
                var client = new RestClient(options);

                var request = new RestRequest("messages");
                request.AddParameter("from", $"Verificación <mailgun@{_mailgunDomain}>");
                request.AddParameter("to", $"user{userId}@example.com");
                request.AddParameter("subject", "Código de Verificación");
                request.AddParameter("text", $"Tu código de verificación es: {verificationCode}. Este código expirará en 10 minutos.");
                request.AddParameter("html", $"<h1>Tu código de verificación es: <strong>{verificationCode}</strong></h1><p>Este código expirará en <strong>10 minutos</strong>.</p>");

                var response = await client.PostAsync(request);

                if (response.IsSuccessful)
                {
                    _logger.LogInformation($"Código de verificación enviado a user{userId}@example.com");
                    return true;
                }
                else
                {
                    _logger.LogError($"Error al enviar código de verificación: {response.ErrorMessage}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al generar o enviar el código de verificación para el usuario {userId}");
                return false;
            }
        }

        private string GenerateVerificationCode()
        {
            // Implementación de la lógica para generar un código de verificación único
            // Puedes usar GUIDs, números aleatorios, o cualquier otro método que se ajuste a tus necesidades
            return Guid.NewGuid().ToString().Substring(0, 6).ToUpper(); // Ejemplo: Devuelve los primeros 6 caracteres de un GUID
        }

        public async Task<bool> VerifyTwoFactorCodeAsync(int userId, string codeToVerify, string clientIp)
        {
            try
            {
                // Agregamos una operación asíncrona para justificar el uso de async/await
                await Task.Delay(1); // Simula una operación asíncrona mínima

                _logger.LogInformation($"Intentando verificar código para usuario {userId}: {codeToVerify}");

                if (VerificationCodes.ContainsKey(userId))
                {
                    var (storedCode, expiration) = VerificationCodes[userId];

                    // Verificar si el código ha expirado
                    if (DateTime.Now > expiration)
                    {
                        _logger.LogWarning($"Código de verificación expirado para el usuario {userId}. Expiró a las {expiration}");
                        return false;
                    }

                    // Verificar si el código es correcto
                    if (storedCode == codeToVerify)
                    {
                        // Eliminar el código después de verificarlo exitosamente
                        VerificationCodes.Remove(userId);

                        _logger.LogInformation($"Código de verificación correcto para el usuario {userId} desde IP {clientIp}");
                        return true;
                    }
                    else
                    {
                        _logger.LogWarning($"Código de verificación incorrecto para el usuario {userId} desde IP {clientIp}. Esperado: {storedCode}, Recibido: {codeToVerify}");
                        return false;
                    }
                }
                else
                {
                    _logger.LogWarning($"No se encontró código de verificación para el usuario {userId}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al verificar el código de verificación para el usuario {userId} desde IP {clientIp}");
                return false;
            }
        }

        private async Task<string> GetUserEmailAsync(int userId)
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand("SELECT correo FROM Usuarios WHERE id_usuario = @userId", connection))
                    {
                        command.Parameters.AddWithValue("@userId", userId);
                        var result = await command.ExecuteScalarAsync();
                        return result?.ToString() ?? string.Empty;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al obtener el correo electrónico del usuario {userId}");
                return string.Empty;
            }
        }
    }
}

