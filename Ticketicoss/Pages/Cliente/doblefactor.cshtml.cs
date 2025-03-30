using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using WebApplication1.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;

namespace WebApplication1.Pages.Cliente
{
    public class TwoFactorAuthModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<TwoFactorAuthModel> _logger;
        private readonly AuthService _authService;

        public TwoFactorAuthModel(IConfiguration configuration, ILogger<TwoFactorAuthModel> logger, AuthService authService)
        {
            _configuration = configuration;
            _logger = logger;
            _authService = authService;
        }

        [BindProperty]
        public string[] VerificationCode { get; set; } = new string[5];

        [BindProperty]
        public string FullVerificationCode { get; set; } = "";

        [TempData]
        public string? ErrorMessage { get; set; }

        [TempData]
        public string? SuccessMessage { get; set; }

        // Propiedad para almacenar la URL de retorno
        [BindProperty(SupportsGet = true)]
        public string ReturnUrl { get; set; } = string.Empty;

        public IActionResult OnGet(string returnUrl = null)
        {
            // Guardar la URL de retorno si se proporciona
            if (!string.IsNullOrEmpty(returnUrl))
            {
                ReturnUrl = returnUrl;
                _logger.LogInformation($"URL de retorno recibida en doblefactor: {ReturnUrl}");
            }

            // Verificar si el usuario está en proceso de login
            if (HttpContext.Session.GetInt32("UserId") == null)
            {
                return RedirectToPage("/Cliente/Login", new { returnUrl = ReturnUrl });
            }

            // Verificar si el usuario requiere autenticación de doble factor
            bool requiresTwoFactor = HttpContext.Session.GetString("RequiresTwoFactor") == "true";
            if (!requiresTwoFactor)
            {
                // Si hay una URL de retorno, redirigir a ella
                if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
                {
                    _logger.LogInformation($"Redirigiendo a URL de retorno: {ReturnUrl}");
                    return Redirect(ReturnUrl);
                }

                // Redirigir según el tipo de usuario
                string tipoUsuario = HttpContext.Session.GetString("UserType") ?? "cliente";
                if (tipoUsuario == "cliente")
                {
                    return RedirectToPage("/Cliente/preventa");
                }
                else
                {
                    return RedirectToPage("/Admin/admincodigospromo");
                }
            }

            // Verificar si ya completó la autenticación de doble factor
            bool twoFactorCompleted = HttpContext.Session.GetString("TwoFactorAuthenticated") == "true";
            if (twoFactorCompleted)
            {
                // Si hay una URL de retorno, redirigir a ella
                if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
                {
                    _logger.LogInformation($"Redirigiendo a URL de retorno: {ReturnUrl}");
                    return Redirect(ReturnUrl);
                }

                // Redirigir según el tipo de usuario
                string tipoUsuario = HttpContext.Session.GetString("UserType") ?? "cliente";
                if (tipoUsuario == "cliente")
                {
                    return RedirectToPage("/Cliente/preventa");
                }
                else
                {
                    return RedirectToPage("/Admin/admincodigospromo");
                }
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                // Verificar si el usuario está en proceso de login
                int? userId = HttpContext.Session.GetInt32("UserId");
                if (!userId.HasValue)
                {
                    return RedirectToPage("/Cliente/Login", new { returnUrl = ReturnUrl });
                }

                // Obtener el código de verificación (ya sea del campo completo o de los campos individuales)
                string code;

                if (!string.IsNullOrEmpty(FullVerificationCode))
                {
                    // Si se usó el campo para pegar el código completo
                    code = FullVerificationCode.Trim();
                }
                else
                {
                    // Combinar los dígitos del código de los campos individuales
                    code = string.Join("", VerificationCode);
                }

                if (string.IsNullOrEmpty(code) || code.Length != 5)
                {
                    ErrorMessage = "Por favor, ingresa el código de verificación completo.";
                    return Page();
                }

                // Obtener la dirección IP del cliente
                string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                // Registrar información para depuración
                _logger.LogInformation($"Verificando código: {code} para usuario: {userId}");

                // Verificar el código
                bool isValid = await _authService.VerifyTwoFactorCodeAsync(userId.Value, code, clientIp);

                if (!isValid)
                {
                    ErrorMessage = "Código de verificación inválido o expirado. Por favor, solicita un nuevo código.";
                    return Page();
                }

                // Marcar al usuario como completamente autenticado
                HttpContext.Session.SetString("TwoFactorAuthenticated", "true");
                HttpContext.Session.Remove("RequiresTwoFactor");

                _logger.LogInformation($"Usuario {userId} completó autenticación de doble factor exitosamente");

                // Si hay una URL de retorno, redirigir a ella
                if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
                {
                    _logger.LogInformation($"Redirigiendo a URL de retorno después de 2FA: {ReturnUrl}");
                    return Redirect(ReturnUrl);
                }

                // Redirigir según el tipo de usuario
                string tipoUsuario = HttpContext.Session.GetString("UserType") ?? "cliente";
                if (tipoUsuario == "cliente")
                {
                    return RedirectToPage("/Cliente/preventa");
                }
                else
                {
                    return RedirectToPage("/Admin/admincodigospromo");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en la verificación de doble factor");
                ErrorMessage = "Ocurrió un error al procesar tu solicitud. Por favor, intenta nuevamente.";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostResendCodeAsync()
        {
            try
            {
                // Verificar si el usuario está en proceso de login
                int? userId = HttpContext.Session.GetInt32("UserId");
                if (!userId.HasValue)
                {
                    return new JsonResult(new { success = false, message = "Sesión inválida. Por favor, inicia sesión nuevamente." });
                }

                _logger.LogInformation($"Solicitando reenvío de código para usuario {userId}");

                // Generar y enviar un nuevo código
                bool codeSent = await _authService.GenerateAndSendTwoFactorCodeAsync(userId.Value);

                if (!codeSent)
                {
                    _logger.LogError($"No se pudo enviar el código para usuario {userId}");
                    return new JsonResult(new { success = false, message = "No se pudo enviar el código. Por favor, intenta nuevamente." });
                }

                _logger.LogInformation($"Código reenviado exitosamente para usuario {userId}");
                return new JsonResult(new { success = true, message = "Se ha enviado un nuevo código a tu correo electrónico. Este código expirará en 10 minutos." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al reenviar código de verificación");
                return new JsonResult(new { success = false, message = "Ocurrió un error al enviar el código." });
            }
        }
    }
}

