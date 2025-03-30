using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WebApplication1.Data;
using WebApplication1.Models;
using Microsoft.AspNetCore.Http;

namespace WebApplication1.Pages.Cliente
{
    public class PreventaModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PreventaModel> _logger;

        public PreventaModel(ApplicationDbContext context, ILogger<PreventaModel> logger)
        {
            _context = context;
            _logger = logger;
            Eventos = new List<Evento>();
            TieneAccesoPreventa = false;
        }

        public List<Evento> Eventos { get; set; }
        public bool TieneAccesoPreventa { get; set; }
        public string MensajeAcceso { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                _logger.LogInformation("Iniciando carga de eventos en preventa");

                // Obtener el ID del usuario de la sesión
                // El filtro de autenticación ya garantiza que el usuario está autenticado
                int? userId = HttpContext.Session.GetInt32("UserId");

                _logger.LogInformation($"Usuario autenticado con ID: {userId.Value}");

                // Verificar si el usuario tiene tarjeta American Express
                bool tieneAmex = false;
                try
                {
                    // Consulta directa a la base de datos para verificar tarjetas
                    var tarjetas = await _context.TarjetasUsuario
                        .Where(t => t.IdUsuario == userId.Value)
                        .ToListAsync();

                    _logger.LogInformation($"Usuario tiene {tarjetas.Count} tarjetas registradas");

                    // Mostrar información de cada tarjeta para depuración
                    foreach (var tarjeta in tarjetas)
                    {
                        _logger.LogInformation($"Tarjeta ID: {tarjeta.IdTarjeta}, Tipo: {tarjeta.TipoTarjeta}, Número: {tarjeta.NumeroTarjeta}, Vencimiento: {tarjeta.Vencimiento}");
                    }

                    // Simplificar la verificación de American Express
                    tieneAmex = tarjetas.Any(t => t.TipoTarjeta == "American Express");

                    _logger.LogInformation($"¿Usuario tiene tarjeta American Express? {tieneAmex}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al verificar tarjetas del usuario");
                    tieneAmex = false;
                }

                TieneAccesoPreventa = tieneAmex;

                // Cargar eventos en preventa independientemente de si tiene tarjeta Amex
                try
                {
                    _logger.LogInformation("Consultando eventos en preventa");

                    // Cargar eventos con sus preventas en una sola consulta
                    var eventosConPreventa = await _context.Eventos
                        .Include(e => e.Preventa)
                        .Where(e => e.Estado == "activo" &&
                                e.Preventa.Any(p => p.Estado == "activa"))
                        .ToListAsync();

                    _logger.LogInformation($"Eventos en preventa encontrados: {eventosConPreventa.Count}");

                    // Mostrar eventos solo si tiene acceso a preventa
                    if (TieneAccesoPreventa)
                    {
                        Eventos = eventosConPreventa;
                        MensajeAcceso = "¡Tienes acceso exclusivo a todas las preventas con tu tarjeta American Express!";
                    }
                    else
                    {
                        // Si no tiene American Express, no mostrar eventos
                        Eventos = new List<Evento>();
                        MensajeAcceso = "Agrega una tarjeta American Express a tu billetera para acceder a preventas exclusivas";
                    }

                    // Después de cargar los eventos, ajustar las rutas de las imágenes
                    foreach (var evento in Eventos)
                    {
                        _logger.LogInformation($"Evento: {evento.IdEvento}, Nombre: {evento.Nombre}, Imagen: {evento.ImagenUrl}");

                        if (!string.IsNullOrEmpty(evento.ImagenUrl) && !evento.ImagenUrl.StartsWith("http"))
                        {
                            // Convertir rutas relativas a rutas basadas en la raíz de la aplicación
                            evento.ImagenUrl = $"~/Content/{evento.ImagenUrl.TrimStart('/')}";
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al consultar eventos en preventa: {Message}", ex.Message);
                    MensajeAcceso = "Error al cargar eventos. Por favor, intenta nuevamente.";
                    Eventos = new List<Evento>();
                }

                // No redirigir a ninguna otra página, simplemente mostrar la página de preventa
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error general al cargar eventos en preventa: {Message}", ex.Message);

                // En caso de error, mostrar una lista vacía
                Eventos = new List<Evento>();
                MensajeAcceso = "Error al cargar eventos. Por favor, intenta nuevamente.";

                return Page();
            }
        }
    }
}

    



