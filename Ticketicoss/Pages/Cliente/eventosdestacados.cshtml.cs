using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace WebApplication1.Pages.Cliente
{
    public class EventosDestacadosModel : PageModel
    {
        private readonly ILogger<EventosDestacadosModel> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public EventosDestacadosModel(ILogger<EventosDestacadosModel> logger, IConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _connectionString = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada.");
        }

        public List<EventoViewModel> Eventos { get; set; } = new List<EventoViewModel>();

        public async Task OnGetAsync()
        {
            try
            {
                await CargarEventosDestacadosAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar los eventos destacados");
            }
        }

        private async Task CargarEventosDestacadosAsync()
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // Consulta para obtener eventos activos
                string query = @"
                    SELECT e.id_evento, e.nombre, e.descripcion, e.fecha, e.lugar, e.imagen_url
                    FROM Eventos e
                    WHERE e.estado = 'activo'
                    ORDER BY e.fecha ASC";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int idEvento = reader.GetInt32(0);
                            string nombre = reader.GetString(1);
                            string descripcion = reader.GetString(2);
                            DateTime fecha = reader.GetDateTime(3);
                            string lugar = reader.GetString(4);
                            string imagenUrl = !reader.IsDBNull(5) ? reader.GetString(5) : "/images/evento-default.jpg";

                            // Crear el modelo de vista para el evento
                            var evento = new EventoViewModel
                            {
                                IdEvento = idEvento,
                                Nombre = nombre,
                                Descripcion = descripcion,
                                Fecha = fecha,
                                Lugar = lugar,
                                ImagenUrl = imagenUrl,
                                // Asumimos una hora predeterminada ya que no está en la base de datos
                                Hora = "20:00"
                            };

                            Eventos.Add(evento);
                        }
                    }
                }

                // Si no hay eventos, intentamos obtener algunos de la tabla de preventas
                if (Eventos.Count == 0)
                {
                    string preventaQuery = @"
                        SELECT e.id_evento, e.nombre, e.descripcion, e.fecha, e.lugar, e.imagen_url
                        FROM Eventos e
                        INNER JOIN Preventa p ON e.id_evento = p.id_evento
                        WHERE p.estado = 'activa'
                        ORDER BY e.fecha ASC";

                    using (SqlCommand command = new SqlCommand(preventaQuery, connection))
                    {
                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                int idEvento = reader.GetInt32(0);
                                string nombre = reader.GetString(1);
                                string descripcion = reader.GetString(2);
                                DateTime fecha = reader.GetDateTime(3);
                                string lugar = reader.GetString(4);
                                string imagenUrl = !reader.IsDBNull(5) ? reader.GetString(5) : "/images/evento-default.jpg";

                                // Crear el modelo de vista para el evento
                                var evento = new EventoViewModel
                                {
                                    IdEvento = idEvento,
                                    Nombre = nombre,
                                    Descripcion = descripcion,
                                    Fecha = fecha,
                                    Lugar = lugar,
                                    ImagenUrl = imagenUrl,
                                    // Asumimos una hora predeterminada ya que no está en la base de datos
                                    Hora = "20:00"
                                };

                                Eventos.Add(evento);
                            }
                        }
                    }
                }
            }
        }
    }

    public class EventoViewModel
    {
        public int IdEvento { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public string Lugar { get; set; } = string.Empty;
        public string ImagenUrl { get; set; } = string.Empty;
        public string Hora { get; set; } = string.Empty;
    }
}