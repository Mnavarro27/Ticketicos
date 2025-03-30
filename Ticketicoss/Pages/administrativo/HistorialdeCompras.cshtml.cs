using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;

namespace WebApplication1.Pages.Admin
{
    public class ModificarBoletoModel : PageModel
    {
        private readonly ILogger<ModificarBoletoModel> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public ModificarBoletoModel(ILogger<ModificarBoletoModel> logger, IConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _connectionString = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada.");
        }

        [BindProperty(SupportsGet = true)]
        public int IdUsuario { get; set; }

        [BindProperty(SupportsGet = true)]
        public int IdCompra { get; set; }

        [BindProperty(SupportsGet = true)]
        public int IdDetalle { get; set; }

        [BindProperty]
        public ModificarBoletoViewModel Boleto { get; set; } = new ModificarBoletoViewModel();

        public string NombreUsuario { get; set; } = string.Empty;
        public string NombreEvento { get; set; } = string.Empty;
        public DateTime FechaEvento { get; set; }
        public string LugarEvento { get; set; } = string.Empty;

        [TempData]
        public string? MensajeExito { get; set; }

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
                    _logger.LogWarning("Intento de acceso no autorizado a la modificación de boleto");
                    return RedirectToPage("/Cliente/Login");
                }

                // Cargar datos del usuario
                bool datosEncontrados = await CargarDatosAsync();

                if (!datosEncontrados)
                {
                    MensajeError = "No se encontraron los datos del boleto.";
                    return RedirectToPage("/Admin/HistorialComprasUsuario", new { idUsuario = IdUsuario });
                }

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar datos para modificar boleto");
                MensajeError = "Ocurrió un error al cargar los datos del boleto.";
                return RedirectToPage("/Admin/HistorialComprasUsuario", new { idUsuario = IdUsuario });
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                // Verificar si el usuario está autenticado y es administrador
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");

                if (!userId.HasValue || userType != "admin")
                {
                    _logger.LogWarning("Intento de acceso no autorizado para modificar boleto");
                    return RedirectToPage("/Cliente/Login");
                }

                if (!ModelState.IsValid)
                {
                    await CargarDatosAsync();
                    return Page();
                }

                // Actualizar los datos del boleto
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // Iniciar una transacción para asegurar la integridad de los datos
                    using (SqlTransaction transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // Actualizar el detalle de la compra
                            string updateDetalleQuery = @"
                              UPDATE DetalleCompra 
                              SET cantidad = @Cantidad, 
                                  subtotal = @Subtotal
                              WHERE id_detalle = @IdDetalle AND id_compra = @IdCompra";

                            using (SqlCommand command = new SqlCommand(updateDetalleQuery, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@Cantidad", Boleto.Cantidad);
                                command.Parameters.AddWithValue("@Subtotal", Boleto.Subtotal);
                                command.Parameters.AddWithValue("@IdDetalle", IdDetalle);
                                command.Parameters.AddWithValue("@IdCompra", IdCompra);

                                int rowsAffected = await command.ExecuteNonQueryAsync();

                                if (rowsAffected == 0)
                                {
                                    transaction.Rollback();
                                    MensajeError = "No se encontró el detalle de la compra.";
                                    await CargarDatosAsync();
                                    return Page();
                                }
                            }

                            // Actualizar el total de la compra
                            string updateCompraQuery = @"
                              UPDATE Compras 
                              SET total = (SELECT SUM(subtotal) FROM DetalleCompra WHERE id_compra = @IdCompra)
                              WHERE id_compra = @IdCompra AND id_usuario = @IdUsuario";

                            using (SqlCommand command = new SqlCommand(updateCompraQuery, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@IdCompra", IdCompra);
                                command.Parameters.AddWithValue("@IdUsuario", IdUsuario);

                                int rowsAffected = await command.ExecuteNonQueryAsync();

                                if (rowsAffected == 0)
                                {
                                    transaction.Rollback();
                                    MensajeError = "No se encontró la compra.";
                                    await CargarDatosAsync();
                                    return Page();
                                }
                            }

                            // Registrar la acción en el log (en lugar de en la base de datos)
                            _logger.LogInformation(
                                "Modificación de boleto: Admin ID {AdminId}, Usuario ID {UserId}, Compra #{CompraId}, Detalle #{DetalleId}",
                                userId.Value, IdUsuario, IdCompra, IdDetalle);

                            // Confirmar la transacción
                            transaction.Commit();
                        }
                        catch (Exception ex)
                        {
                            // Revertir la transacción en caso de error
                            transaction.Rollback();
                            _logger.LogError(ex, "Error en la transacción al modificar boleto");
                            throw;
                        }
                    }
                }

                MensajeExito = "Boleto modificado exitosamente.";
                return RedirectToPage("/Admin/HistorialComprasUsuario", new { idUsuario = IdUsuario });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al modificar boleto");
                MensajeError = "Ocurrió un error al modificar el boleto.";
                await CargarDatosAsync();
                return Page();
            }
        }

        private async Task<bool> CargarDatosAsync()
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // Obtener datos del usuario
                string queryUsuario = "SELECT nombre, apellidos FROM Usuarios WHERE id_usuario = @IdUsuario";

                using (SqlCommand command = new SqlCommand(queryUsuario, connection))
                {
                    command.Parameters.AddWithValue("@IdUsuario", IdUsuario);

                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            string nombre = reader.GetString(0);
                            string apellidos = reader.GetString(1);
                            NombreUsuario = $"{nombre} {apellidos}";
                        }
                        else
                        {
                            return false;
                        }
                    }
                }

                // Obtener datos del boleto
                string queryBoleto = @"
                  SELECT dc.cantidad, dc.subtotal, 
                         e.nombre as nombre_evento, e.fecha as fecha_evento, e.lugar as lugar_evento,
                         b.categoria, b.precio
                  FROM DetalleCompra dc
                  INNER JOIN Boletos b ON dc.id_boleto = b.id_boleto
                  INNER JOIN Eventos e ON b.id_evento = e.id_evento
                  WHERE dc.id_detalle = @IdDetalle AND dc.id_compra = @IdCompra";

                using (SqlCommand command = new SqlCommand(queryBoleto, connection))
                {
                    command.Parameters.AddWithValue("@IdDetalle", IdDetalle);
                    command.Parameters.AddWithValue("@IdCompra", IdCompra);

                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            int cantidad = reader.GetInt32(0);
                            decimal subtotal = reader.GetDecimal(1);
                            NombreEvento = reader.GetString(2);
                            FechaEvento = reader.GetDateTime(3);
                            LugarEvento = reader.GetString(4);
                            string categoria = reader.GetString(5);
                            decimal precio = reader.GetDecimal(6);

                            Boleto.Cantidad = cantidad;
                            Boleto.Subtotal = subtotal;
                            Boleto.Categoria = categoria;
                            Boleto.PrecioUnitario = precio;

                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }

    public class ModificarBoletoViewModel
    {
        [Required(ErrorMessage = "La cantidad es obligatoria")]
        [Range(1, 10, ErrorMessage = "La cantidad debe estar entre 1 y 10")]
        public int Cantidad { get; set; }

        [Required(ErrorMessage = "El subtotal es obligatorio")]
        [Range(1, 1000000, ErrorMessage = "El subtotal debe ser un valor positivo")]
        public decimal Subtotal { get; set; }

        public string Categoria { get; set; } = string.Empty;

        public decimal PrecioUnitario { get; set; }
    }
}

