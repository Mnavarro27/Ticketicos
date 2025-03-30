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
    public class BilleteraModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<BilleteraModel> _logger;

        public BilleteraModel(ApplicationDbContext context, ILogger<BilleteraModel> logger)
        {
            _context = context;
            _logger = logger;
            TarjetasUsuario = new List<WebApplication1.Models.TarjetaUsuario>(); // Uso completo de la clase
            MensajeExito = string.Empty;
            MensajeError = string.Empty;
            NuevaTarjeta = new WebApplication1.Models.TarjetaUsuario(); // Uso completo de la clase
        }

        public List<WebApplication1.Models.TarjetaUsuario> TarjetasUsuario { get; set; }
        public string? MensajeExito { get; set; }
        public string? MensajeError { get; set; }

        [BindProperty]
        public WebApplication1.Models.TarjetaUsuario NuevaTarjeta { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                // Obtener el ID del usuario de la sesión
                int? userId = HttpContext.Session.GetInt32("UserId");

                // Si no está en la sesión, intentar obtenerlo de la cookie
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

                // Si aún no está autenticado, redirigir a login
                if (!userId.HasValue)
                {
                    return RedirectToPage("/Cliente/Login");
                }

                // Cargar las tarjetas del usuario
                TarjetasUsuario = await _context.TarjetasUsuario
                    .Where(t => t.IdUsuario == userId.Value) // Ahora es seguro acceder a .Value
                    .ToListAsync();

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar la billetera virtual: {Message}", ex.Message);
                MensajeError = "Error al cargar las tarjetas. Por favor, intenta nuevamente.";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostAgregarTarjetaAsync()
        {
            try
            {
                // Obtener el ID del usuario de la sesión
                int? userId = HttpContext.Session.GetInt32("UserId");

                // Si no está en la sesión, intentar obtenerlo de la cookie
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

                // Si aún no está autenticado, redirigir a login
                if (!userId.HasValue)
                {
                    return RedirectToPage("/Cliente/Login");
                }

                if (!ModelState.IsValid)
                {
                    // Recargar las tarjetas del usuario
                    TarjetasUsuario = await _context.TarjetasUsuario
                        .Where(t => t.IdUsuario == userId.Value) // Ahora es seguro acceder a .Value
                        .ToListAsync();

                    return Page();
                }

                // Configurar la nueva tarjeta
                NuevaTarjeta.IdUsuario = userId.Value; // Ahora es seguro acceder a .Value

                // Guardar la tarjeta en la base de datos
                _context.TarjetasUsuario.Add(NuevaTarjeta);
                await _context.SaveChangesAsync();

                MensajeExito = "Tarjeta agregada exitosamente a tu billetera virtual.";

                // Redirigir a la misma página para mostrar la tarjeta agregada
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al agregar tarjeta: {Message}", ex.Message);

                // Recargar las tarjetas del usuario
                int? userId = HttpContext.Session.GetInt32("UserId");
                if (userId.HasValue)
                {
                    TarjetasUsuario = await _context.TarjetasUsuario
                        .Where(t => t.IdUsuario == userId.Value) // Ahora es seguro acceder a .Value
                        .ToListAsync();
                }

                MensajeError = "Error al agregar la tarjeta. Por favor, intenta nuevamente.";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostEliminarTarjetaAsync(int idTarjeta)
        {
            try
            {
                // Obtener el ID del usuario de la sesión
                int? userId = HttpContext.Session.GetInt32("UserId");

                // Si no está en la sesión, intentar obtenerlo de la cookie
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

                // Si aún no está autenticado, redirigir a login
                if (!userId.HasValue)
                {
                    return RedirectToPage("/Cliente/Login");
                }

                // Buscar la tarjeta
                var tarjeta = await _context.TarjetasUsuario
                    .FirstOrDefaultAsync(t => t.IdTarjeta == idTarjeta && t.IdUsuario == userId.Value); // Ahora es seguro acceder a .Value

                if (tarjeta != null)
                {
                    _context.TarjetasUsuario.Remove(tarjeta);
                    await _context.SaveChangesAsync();
                    MensajeExito = "Tarjeta eliminada exitosamente de tu billetera virtual.";
                }

                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar tarjeta: {Message}", ex.Message);
                MensajeError = "Error al eliminar la tarjeta. Por favor, intenta nuevamente.";
                return RedirectToPage();
            }
        }
    }
}