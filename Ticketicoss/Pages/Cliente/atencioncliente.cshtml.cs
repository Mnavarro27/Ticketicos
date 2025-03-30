using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace WebApplication1.Pages.Cliente
{
    public class AtencionClienteModel : PageModel
    {
        private readonly ILogger<AtencionClienteModel> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public AtencionClienteModel(ILogger<AtencionClienteModel> logger, IConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _connectionString = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada.");
        }

        public List<PreguntaFrecuenteViewModel> PreguntasFrecuentes { get; set; } = new List<PreguntaFrecuenteViewModel>();
        public List<TicketSoporteViewModel> TicketsUsuario { get; set; } = new List<TicketSoporteViewModel>();

        [BindProperty(SupportsGet = true)]
        public string TerminoBusqueda { get; set; } = string.Empty;

        public string MensajeError { get; set; } = string.Empty;

        public async Task OnGetAsync()
        {
            try
            {
                // Obtener el ID del usuario de la sesión
                // El filtro de autenticación ya garantiza que el usuario está autenticado
                int? userId = HttpContext.Session.GetInt32("UserId");

                // NUEVO: Si no está en la sesión, intentar obtenerlo de la cookie
                if (!userId.HasValue)
                {
                    string? userIdStr = Request.Cookies["UserId"];
                    if (!string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out int cookieUserId))
                    {
                        // Si se encuentra en la cookie, restaurar a la sesión también
                        userId = cookieUserId;
                        HttpContext.Session.SetInt32("UserId", cookieUserId);
                        _logger.LogInformation($"Usuario {userId} autenticado desde cookie");
                    }
                }

                // Cargar preguntas frecuentes
                await CargarPreguntasFrecuentesAsync();

                // Si el usuario está autenticado, cargar sus tickets
                if (userId.HasValue)
                {
                    await CargarTicketsUsuarioAsync(userId.Value);
                }
                else
                {
                    // Si no está autenticado, redirigir a login
                    Response.Redirect("/Cliente/Login");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar la página de atención al cliente");
                MensajeError = "Ocurrió un error al cargar la información. Por favor, intenta nuevamente.";
            }
        }

        public async Task<IActionResult> OnPostBuscarAsync()
        {
            if (string.IsNullOrWhiteSpace(TerminoBusqueda))
            {
                return RedirectToPage();
            }

            try
            {
                await CargarPreguntasFrecuentesAsync(TerminoBusqueda);

                // Obtener el ID del usuario de la sesión
                // El filtro de autenticación ya garantiza que el usuario está autenticado
                int? userId = HttpContext.Session.GetInt32("UserId");

                // NUEVO: Si no está en la sesión, intentar obtenerlo de la cookie
                if (!userId.HasValue)
                {
                    string? userIdStr = Request.Cookies["UserId"];
                    if (!string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out int cookieUserId))
                    {
                        // Si se encuentra en la cookie, restaurar a la sesión también
                        userId = cookieUserId;
                        HttpContext.Session.SetInt32("UserId", cookieUserId);
                        _logger.LogInformation($"Usuario {userId} autenticado desde cookie");
                    }
                }

                // Si el usuario está autenticado, cargar sus tickets
                if (userId.HasValue)
                {
                    await CargarTicketsUsuarioAsync(userId.Value);
                }
                else
                {
                    // Si no está autenticado, redirigir a login
                    return RedirectToPage("/Cliente/Login");
                }

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al buscar preguntas frecuentes");
                MensajeError = "Ocurrió un error al realizar la búsqueda. Por favor, intenta nuevamente.";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostCrearTicketAsync([FromBody] NuevoTicketModel nuevoTicket)
        {
            if (string.IsNullOrWhiteSpace(nuevoTicket.Mensaje))
            {
                return new JsonResult(new { success = false, message = "El mensaje no puede estar vacío." });
            }

            try
            {
                // Obtener el ID del usuario de la sesión
                // El filtro de autenticación ya garantiza que el usuario está autenticado
                int? userId = HttpContext.Session.GetInt32("UserId");

                // NUEVO: Si no está en la sesión, intentar obtenerlo de la cookie
                if (!userId.HasValue)
                {
                    string? userIdStr = Request.Cookies["UserId"];
                    if (!string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out int cookieUserId))
                    {
                        // Si se encuentra en la cookie, restaurar a la sesión también
                        userId = cookieUserId;
                        HttpContext.Session.SetInt32("UserId", cookieUserId);
                        _logger.LogInformation($"Usuario {userId} autenticado desde cookie");
                    }
                }

                // MODIFICADO: Verificar si userId tiene valor antes de acceder a .Value
                if (!userId.HasValue)
                {
                    return new JsonResult(new { success = false, message = "Debes iniciar sesión para crear un ticket." });
                }

                // Crear el ticket en la base de datos
                bool ticketCreado = await CrearTicketAsync(userId.Value, nuevoTicket.Mensaje);

                if (ticketCreado)
                {
                    return new JsonResult(new { success = true, message = "Ticket creado exitosamente." });
                }
                else
                {
                    return new JsonResult(new { success = false, message = "No se pudo crear el ticket. Intenta nuevamente." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear ticket");
                return new JsonResult(new { success = false, message = "Ocurrió un error al crear el ticket." });
            }
        }

        private async Task CargarPreguntasFrecuentesAsync(string terminoBusqueda = "")
        {
            PreguntasFrecuentes.Clear();

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                string query;
                SqlCommand command;

                if (string.IsNullOrWhiteSpace(terminoBusqueda))
                {
                    // Obtener las preguntas frecuentes
                    query = @"
                        SELECT TOP 4 id_pregunta, pregunta, respuesta, fecha
                        FROM PreguntasFrecuentes
                        ORDER BY fecha DESC";

                    command = new SqlCommand(query, connection);
                }
                else
                {
                    // Buscar preguntas frecuentes por término
                    query = @"
                        SELECT id_pregunta, pregunta, respuesta, fecha
                        FROM PreguntasFrecuentes
                        WHERE pregunta LIKE @TerminoBusqueda
                        ORDER BY fecha DESC";

                    command = new SqlCommand(query, connection);
                    command.Parameters.AddWithValue("@TerminoBusqueda", $"%{terminoBusqueda}%");
                }

                using (SqlDataReader reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        int idPregunta = reader.GetInt32(0);
                        string pregunta = reader.GetString(1);
                        string respuesta = reader.GetString(2);
                        DateTime fecha = reader.GetDateTime(3);

                        var preguntaFrecuente = new PreguntaFrecuenteViewModel
                        {
                            Id = idPregunta,
                            Pregunta = pregunta,
                            Respuesta = respuesta,
                            Fecha = fecha
                        };

                        PreguntasFrecuentes.Add(preguntaFrecuente);
                    }
                }
            }
        }

        private async Task CargarTicketsUsuarioAsync(int userId)
        {
            TicketsUsuario.Clear();

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                string query = @"
                    SELECT id_ticket, mensaje, estado, fecha
                    FROM Soporte
                    WHERE id_usuario = @UserId
                    ORDER BY fecha DESC";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@UserId", userId);

                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int idTicket = reader.GetInt32(0);
                            string mensaje = reader.GetString(1);
                            string estado = reader.GetString(2);
                            DateTime fecha = reader.GetDateTime(3);

                            var ticket = new TicketSoporteViewModel
                            {
                                Id = idTicket,
                                Mensaje = mensaje,
                                Estado = estado,
                                Fecha = fecha
                            };

                            TicketsUsuario.Add(ticket);
                        }
                    }
                }
            }
        }

        private async Task<bool> CrearTicketAsync(int userId, string mensaje)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                string query = @"
                    INSERT INTO Soporte (id_usuario, mensaje, estado, fecha)
                    VALUES (@UserId, @Mensaje, 'abierto', @Fecha)";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@UserId", userId);
                    command.Parameters.AddWithValue("@Mensaje", mensaje);
                    command.Parameters.AddWithValue("@Fecha", DateTime.Now);

                    int rowsAffected = await command.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }
    }

    public class PreguntaFrecuenteViewModel
    {
        public int Id { get; set; }
        public string Pregunta { get; set; } = string.Empty;
        public string Respuesta { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
    }

    public class TicketSoporteViewModel
    {
        public int Id { get; set; }
        public string Mensaje { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
    }

    public class NuevoTicketModel
    {
        public string Mensaje { get; set; } = string.Empty;
    }
}