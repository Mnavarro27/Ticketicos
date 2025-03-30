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
    public class CodigosPromoModel : PageModel
    {
        private readonly ILogger<CodigosPromoModel> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public CodigosPromoModel(ILogger<CodigosPromoModel> logger, IConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _connectionString = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("La cadena de conexi�n 'DefaultConnection' no est� configurada.");
        }

        public List<PromocionViewModel> Promociones { get; set; } = new List<PromocionViewModel>();

        public async Task OnGetAsync()
        {
            try
            {
                await CargarCodigosPromocionalesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar los c�digos promocionales");
            }
        }

        private async Task CargarCodigosPromocionalesAsync()
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // Consulta para obtener c�digos promocionales activos
                string query = @"
                    SELECT id_codigo, codigo, descuento, fecha_expiracion, estado
                    FROM CodigosPromocionales
                    WHERE estado = 'activo' AND fecha_expiracion >= GETDATE()
                    ORDER BY fecha_expiracion ASC";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int id = reader.GetInt32(0);
                            string codigo = reader.GetString(1);
                            decimal descuento = reader.GetDecimal(2);
                            DateTime fechaExpiracion = reader.GetDateTime(3);

                            // Calcular d�as restantes
                            int diasRestantes = (fechaExpiracion - DateTime.Now).Days;

                            // Crear el modelo de vista para la promoci�n
                            var promocion = new PromocionViewModel
                            {
                                Id = id,
                                Descuento = $"{descuento}% de descuento",
                                Validez = $"V�lido hasta: {fechaExpiracion.ToString("dd/MM/yyyy")} ({diasRestantes} d�as restantes)",
                                Precio = $"C�digo: {codigo}",
                                EsPreventa = diasRestantes <= 7 // Marcar como "especial" si quedan pocos d�as
                            };

                            Promociones.Add(promocion);
                        }
                    }
                }
            }
        }
    }

    public class PromocionViewModel
    {
        public int Id { get; set; }
        public string Descuento { get; set; } = string.Empty; // Porcentaje de descuento
        public string Validez { get; set; } = string.Empty;   // Fecha de expiraci�n
        public string Precio { get; set; } = string.Empty;    // C�digo promocional
        public bool EsPreventa { get; set; }                  // Indica si est� por expirar pronto
    }
}