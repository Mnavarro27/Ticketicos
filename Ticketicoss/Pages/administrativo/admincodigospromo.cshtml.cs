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
    public class AdminCodigosPromoModel : PageModel
    {
        private readonly ILogger<AdminCodigosPromoModel> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public AdminCodigosPromoModel(ILogger<AdminCodigosPromoModel> logger, IConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _connectionString = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("La cadena de conexi�n 'DefaultConnection' no est� configurada.");
        }

        public List<CodigoPromoViewModel> CodigosPromo { get; set; } = new List<CodigoPromoViewModel>();

        [TempData]
        public string? MensajeExito { get; set; }

        [TempData]
        public string? MensajeError { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                // Verificar si el usuario est� autenticado y es administrador
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");

                if (!userId.HasValue || userType != "admin")
                {
                    _logger.LogWarning("Intento de acceso no autorizado a la administraci�n de c�digos promocionales");
                    return RedirectToPage("/Cliente/Login");
                }

                await CargarCodigosPromocionalesAsync();
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar la p�gina de administraci�n de c�digos promocionales");
                MensajeError = "Ocurri� un error al cargar los c�digos promocionales.";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostEliminarCodigoAsync(int id)
        {
            try
            {
                // Verificar si el usuario est� autenticado y es administrador
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");

                if (!userId.HasValue || userType != "admin")
                {
                    _logger.LogWarning("Intento de acceso no autorizado para eliminar c�digo promocional");
                    return RedirectToPage("/Cliente/Login");
                }

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Verificar si el c�digo est� siendo utilizado en alguna compra
                    string checkQuery = @"
                        SELECT COUNT(*) FROM Compras 
                        WHERE id_codigo_promocional = @Id";

                    using (SqlCommand checkCommand = new SqlCommand(checkQuery, connection))
                    {
                        checkCommand.Parameters.AddWithValue("@Id", id);
                        int count = (int)await checkCommand.ExecuteScalarAsync();

                        if (count > 0)
                        {
                            MensajeError = "No se puede eliminar el c�digo promocional porque est� siendo utilizado en compras.";
                            return RedirectToPage();
                        }
                    }

                    // Eliminar el c�digo promocional
                    string deleteQuery = @"
                        DELETE FROM CodigosPromocionales 
                        WHERE id_codigo = @Id";

                    using (SqlCommand deleteCommand = new SqlCommand(deleteQuery, connection))
                    {
                        deleteCommand.Parameters.AddWithValue("@Id", id);
                        int rowsAffected = await deleteCommand.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            MensajeExito = "C�digo promocional eliminado exitosamente.";
                        }
                        else
                        {
                            MensajeError = "No se encontr� el c�digo promocional.";
                        }
                    }
                }

                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar c�digo promocional");
                MensajeError = "Ocurri� un error al eliminar el c�digo promocional.";
                return RedirectToPage();
            }
        }

        public async Task<IActionResult> OnPostActualizarCodigoAsync(int id, string codigo)
        {
            try
            {
                // Verificar si el usuario est� autenticado y es administrador
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");

                if (!userId.HasValue || userType != "admin")
                {
                    _logger.LogWarning("Intento de acceso no autorizado para actualizar c�digo promocional");
                    return RedirectToPage("/Cliente/Login");
                }

                if (string.IsNullOrWhiteSpace(codigo))
                {
                    MensajeError = "El c�digo promocional no puede estar vac�o.";
                    return RedirectToPage();
                }

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Verificar si el c�digo ya existe
                    string checkQuery = @"
                        SELECT COUNT(*) FROM CodigosPromocionales 
                        WHERE codigo = @Codigo AND id_codigo != @Id";

                    using (SqlCommand checkCommand = new SqlCommand(checkQuery, connection))
                    {
                        checkCommand.Parameters.AddWithValue("@Codigo", codigo);
                        checkCommand.Parameters.AddWithValue("@Id", id);
                        int count = (int)await checkCommand.ExecuteScalarAsync();

                        if (count > 0)
                        {
                            MensajeError = "El c�digo promocional ya existe.";
                            return RedirectToPage();
                        }
                    }

                    // Actualizar el c�digo promocional
                    string updateQuery = @"
                        UPDATE CodigosPromocionales 
                        SET codigo = @Codigo 
                        WHERE id_codigo = @Id";

                    using (SqlCommand updateCommand = new SqlCommand(updateQuery, connection))
                    {
                        updateCommand.Parameters.AddWithValue("@Codigo", codigo);
                        updateCommand.Parameters.AddWithValue("@Id", id);
                        int rowsAffected = await updateCommand.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            MensajeExito = "C�digo promocional actualizado exitosamente.";
                        }
                        else
                        {
                            MensajeError = "No se encontr� el c�digo promocional.";
                        }
                    }
                }

                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar c�digo promocional");
                MensajeError = "Ocurri� un error al actualizar el c�digo promocional.";
                return RedirectToPage();
            }
        }

        private async Task CargarCodigosPromocionalesAsync()
        {
            CodigosPromo.Clear();

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                string query = @"
                    SELECT id_codigo, codigo, descuento, fecha_expiracion, estado
                    FROM CodigosPromocionales
                    ORDER BY fecha_expiracion DESC";

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
                            string estado = reader.GetString(4);

                            // Calcular d�as restantes
                            int diasRestantes = (fechaExpiracion - DateTime.Now).Days;
                            string validez = diasRestantes > 0
                                ? $"V�lido hasta: {fechaExpiracion:dd/MM/yyyy} ({diasRestantes} d�as restantes)"
                                : "Expirado";

                            // Crear el modelo de vista para el c�digo promocional
                            var codigoPromo = new CodigoPromoViewModel
                            {
                                Id = id,
                                Codigo = codigo,
                                PorcentajeDescuento = (int)descuento,
                                Monto = descuento,
                                Validez = validez,
                                Descripcion = ObtenerDescripcionPorCodigo(codigo),
                                Estado = estado
                            };

                            CodigosPromo.Add(codigoPromo);
                        }
                    }
                }
            }
        }

        private string ObtenerDescripcionPorCodigo(string codigo)
        {
            // L�gica para determinar la descripci�n basada en el c�digo
            if (codigo.Contains("2x1", StringComparison.OrdinalIgnoreCase))
            {
                return "V�lido para cualquier evento";
            }
            else if (codigo.Contains("DESCUENTO", StringComparison.OrdinalIgnoreCase))
            {
                return "V�lido para conciertos";
            }
            else if (codigo.Contains("DEPORTE", StringComparison.OrdinalIgnoreCase))
            {
                return "V�lido para eventos deportivos";
            }

            return string.Empty;
        }
    }

    public class CodigoPromoViewModel
    {
        public int Id { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public int PorcentajeDescuento { get; set; }
        public decimal Monto { get; set; }
        public string Validez { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
    }
}

