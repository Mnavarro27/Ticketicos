using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;

namespace WebApplication1.Pages.Admin
{
    public class AdminPreventaModel : PageModel
    {
        private readonly ILogger<AdminPreventaModel> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public AdminPreventaModel(ILogger<AdminPreventaModel> logger, IConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _connectionString = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada.");
        }

        public List<PreventaViewModel> Preventas { get; set; } = new List<PreventaViewModel>();

        [TempData]
        public string? MensajeExito { get; set; }

        [TempData]
        public string? MensajeError { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                // Verificar si el usuario está autenticado y es administrador
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");

                if (!userId.HasValue || userType != "admin")
                {
                    _logger.LogWarning("Intento de acceso no autorizado a la administración de preventas");
                    return RedirectToPage("/Cliente/Login");
                }

                await CargarPreventasAsync();
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar la página de administración de preventas");
                MensajeError = "Ocurrió un error al cargar las preventas.";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostEliminarPreventaAsync(int id)
        {
            try
            {
                // Verificar si el usuario está autenticado y es administrador
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");

                if (!userId.HasValue || userType != "admin")
                {
                    _logger.LogWarning("Intento de acceso no autorizado para eliminar preventa");
                    return RedirectToPage("/Cliente/Login");
                }

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Verificar si hay compras asociadas a esta preventa
                    string checkQuery = @"
                        SELECT COUNT(*) FROM Compras c
                        INNER JOIN DetalleCompra dc ON c.id_compra = dc.id_compra
                        INNER JOIN Boletos b ON dc.id_boleto = b.id_boleto
                        WHERE b.id_preventa = @Id";

                    using (SqlCommand checkCommand = new SqlCommand(checkQuery, connection))
                    {
                        checkCommand.Parameters.AddWithValue("@Id", id);
                        int count = (int)await checkCommand.ExecuteScalarAsync();

                        if (count > 0)
                        {
                            MensajeError = "No se puede eliminar la preventa porque ya hay compras asociadas.";
                            return RedirectToPage();
                        }
                    }

                    // Actualizar el estado de la preventa a 'cancelada' en lugar de eliminarla
                    string updateQuery = @"
                        UPDATE Preventa 
                        SET estado = 'cancelada' 
                        WHERE id_preventa = @Id";

                    using (SqlCommand updateCommand = new SqlCommand(updateQuery, connection))
                    {
                        updateCommand.Parameters.AddWithValue("@Id", id);
                        int rowsAffected = await updateCommand.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            MensajeExito = "Preventa cancelada exitosamente.";
                        }
                        else
                        {
                            MensajeError = "No se encontró la preventa.";
                        }
                    }
                }

                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar preventa");
                MensajeError = "Ocurrió un error al eliminar la preventa.";
                return RedirectToPage();
            }
        }

        private async Task CargarPreventasAsync()
        {
            Preventas.Clear();

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                string query = @"
                    SELECT p.id_preventa, e.id_evento, e.nombre, e.fecha, e.lugar, e.imagen_url, 
                           p.fecha_inicio, p.fecha_fin, p.estado,
                           (SELECT COUNT(*) FROM Boletos WHERE id_preventa = p.id_preventa) AS boletos_disponibles
                    FROM Preventa p
                    INNER JOIN Eventos e ON p.id_evento = e.id_evento
                    WHERE p.estado = 'activa' AND e.estado = 'activo'
                    ORDER BY e.fecha ASC";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int id = reader.GetInt32(0);
                            int idEvento = reader.GetInt32(1);
                            string nombre = reader.GetString(2);
                            DateTime fecha = reader.GetDateTime(3);
                            string lugar = reader.GetString(4);
                            string imagenUrl = !reader.IsDBNull(5) ? reader.GetString(5) : "/images/default-event.jpg";
                            DateTime fechaInicio = reader.GetDateTime(6);
                            DateTime fechaFin = reader.GetDateTime(7);
                            string estado = reader.GetString(8);
                            int boletosDisponibles = reader.GetInt32(9);

                            // Crear el modelo de vista para la preventa
                            var preventa = new PreventaViewModel
                            {
                                Id = id,
                                IdEvento = idEvento,
                                Nombre = nombre,
                                Fecha = fecha,
                                Hora = fecha.ToString("HH:mm"),
                                Lugar = lugar,
                                ImagenUrl = imagenUrl.StartsWith("http") ? imagenUrl : $"~/Content/{imagenUrl.TrimStart('/')}",
                                FechaInicio = fechaInicio,
                                FechaFin = fechaFin,
                                Estado = estado,
                                BoletosDisponibles = boletosDisponibles
                            };

                            Preventas.Add(preventa);
                        }
                    }
                }
            }
        }
    }

    public class PreventaViewModel
    {
        public int Id { get; set; }
        public int IdEvento { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public string Hora { get; set; } = string.Empty;
        public string Lugar { get; set; } = string.Empty;
        public string ImagenUrl { get; set; } = string.Empty;
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public string Estado { get; set; } = string.Empty;
        public int BoletosDisponibles { get; set; }
    }
}

