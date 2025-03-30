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
    public class LoginModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<LoginModel> _logger;
        private readonly AuthService _authService;

        public LoginModel(IConfiguration configuration, ILogger<LoginModel> logger, AuthService authService)
        {
            _configuration = configuration;
            _logger = logger;
            _authService = authService;
        }

        [BindProperty]
        public string Correo { get; set; } = string.Empty;

        [BindProperty]
        public string Contraseña { get; set; } = string.Empty;

        [TempData]
        public string? ErrorMessage { get; set; }

        // Propiedad para almacenar la URL de retorno
        [BindProperty(SupportsGet = true)]
        public string ReturnUrl { get; set; } = string.Empty;

        public IActionResult OnGet(string returnUrl = null)
        {
            // Guardar la URL de retorno si se proporciona
            if (!string.IsNullOrEmpty(returnUrl))
            {
                ReturnUrl = returnUrl;
                _logger.LogInformation($"URL de retorno recibida: {ReturnUrl}");
            }

            // Si el usuario ya está autenticado
            int? userId = HttpContext.Session.GetInt32("UserId");
            string? twoFactorAuth = HttpContext.Session.GetString("TwoFactorAuthenticated");

            if (userId.HasValue && twoFactorAuth == "true")
            {
                _logger.LogInformation($"Usuario {userId} ya autenticado, verificando URL de retorno");

                // Si hay una URL de retorno, redirigir a ella
                if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
                {
                    _logger.LogInformation($"Redirigiendo a URL de retorno: {ReturnUrl}");
                    return Redirect(ReturnUrl);
                }

                // Si no hay URL de retorno, redirigir según el tipo de usuario
                string tipoUsuario = GetUserType(userId.Value);

                if (tipoUsuario == "cliente")
                {
                    return RedirectToPage("/Cliente/preventa");
                }
                else
                {
                    return RedirectToPage("/Admin/admincodigospromo");
                }
            }

            // Verificar si hay una cookie de sesión persistente
            string? userIdStr = Request.Cookies["UserId"];
            if (!string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out int cookieUserId))
            {
                // Restaurar la sesión desde la cookie
                HttpContext.Session.SetInt32("UserId", cookieUserId);
                HttpContext.Session.SetString("TwoFactorAuthenticated", "true");

                // Obtener el tipo de usuario para redirigir correctamente
                string tipoUsuario = GetUserType(cookieUserId);
                HttpContext.Session.SetString("UserType", tipoUsuario);

                _logger.LogInformation($"Usuario {cookieUserId} autenticado desde cookie");

                // Si hay una URL de retorno, redirigir a ella
                if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
                {
                    _logger.LogInformation($"Redirigiendo a URL de retorno: {ReturnUrl}");
                    return Redirect(ReturnUrl);
                }

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
                if (string.IsNullOrEmpty(Correo) || string.IsNullOrEmpty(Contraseña))
                {
                    ErrorMessage = "Por favor, ingresa tu correo y contraseña.";
                    return Page();
                }

                // Obtener la dirección IP del cliente
                string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                // Validar credenciales
                var (userId, requiresTwoFactor) = await _authService.ValidateCredentialsAsync(Correo, Contraseña, clientIp);

                if (!userId.HasValue)
                {
                    ErrorMessage = "Correo o contraseña incorrectos.";
                    return Page();
                }

                // Guardar ID de usuario en la sesión
                HttpContext.Session.SetInt32("UserId", userId.Value);
                HttpContext.Session.SetString("UserEmail", Correo);

                // Guardar ID de usuario en una cookie persistente
                Response.Cookies.Append("UserId", userId.Value.ToString(), new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTime.Now.AddDays(7)
                });

                // Obtener el tipo de usuario
                string tipoUsuario = GetUserType(userId.Value);
                HttpContext.Session.SetString("UserType", tipoUsuario);

                // Si el usuario tiene habilitada la autenticación de doble factor
                if (requiresTwoFactor)
                {
                    // Establecer la bandera RequiresTwoFactor en la sesión
                    HttpContext.Session.SetString("RequiresTwoFactor", "true");

                    // Generar y enviar código de verificación
                    bool codeSent = await _authService.GenerateAndSendTwoFactorCodeAsync(userId.Value);

                    if (!codeSent)
                    {
                        ErrorMessage = "No se pudo enviar el código de verificación. Por favor, intenta nuevamente.";
                        return Page();
                    }

                    // Redirigir a la página de verificación de doble factor con la URL de retorno
                    return RedirectToPage("/Cliente/doblefactor", new { returnUrl = ReturnUrl });
                }
                else
                {
                    // Si no requiere doble factor, marcar como autenticado
                    HttpContext.Session.SetString("TwoFactorAuthenticated", "true");

                    // Registrar el inicio de sesión exitoso en el log
                    _logger.LogInformation($"Usuario {userId.Value} autenticado exitosamente. Tipo: {tipoUsuario}");

                    // Si hay una URL de retorno, redirigir a ella
                    if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
                    {
                        _logger.LogInformation($"Redirigiendo a URL de retorno: {ReturnUrl}");
                        return Redirect(ReturnUrl);
                    }

                    // Si no hay URL de retorno, redirigir según el tipo de usuario
                    if (tipoUsuario == "cliente")
                    {
                        return RedirectToPage("/Cliente/preventa");
                    }
                    else
                    {
                        return RedirectToPage("/Admin/admincodigospromo");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en el proceso de login");
                ErrorMessage = "Ocurrió un error al procesar tu solicitud. Por favor, intenta nuevamente.";
                return Page();
            }
        }

        private string GetUserType(int userId)
        {
            try
            {
                string? connectionString = _configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrEmpty(connectionString))
                {
                    _logger.LogError("La cadena de conexión 'DefaultConnection' no está configurada");
                    return "cliente";
                }

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = "SELECT tipo_usuario FROM Usuarios WHERE id_usuario = @UserId";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@UserId", userId);
                        object? result = command.ExecuteScalar();
                        return result?.ToString() ?? "cliente";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al obtener el tipo de usuario {userId}");
                return "cliente"; // Valor predeterminado en caso de error
            }
        }
    }
}


