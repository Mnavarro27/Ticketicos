using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WebApplication1.Services;

namespace WebApplication1.Pages.Cliente
{
    public class PagoEntradasModel : PageModel
    {
        private readonly ILogger<PagoEntradasModel> _logger;
        private readonly IConfiguration _configuration;
        private readonly EmailService _emailService;
        private readonly string _connectionString;

        public PagoEntradasModel(ILogger<PagoEntradasModel> logger, IConfiguration configuration, EmailService emailService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
            _connectionString = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada.");
        }

        [BindProperty(SupportsGet = true)]
        public int EventoId { get; set; }

        [BindProperty]
        public DatosPagoEntradasViewModel DatosPago { get; set; } = new DatosPagoEntradasViewModel();

        public ResumenCompraEntradasViewModel ResumenCompra { get; set; } = new ResumenCompraEntradasViewModel();

        public decimal PrecioTotal { get; set; }
        public decimal PrecioPorBoleto { get; set; }

        public bool MostrarMensajeExito { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (EventoId <= 0)
            {
                return RedirectToPage("/Cliente/EventosDestacados");
            }

            try
            {
                await CargarDatosEventoAsync();

                // Inicializar valores por defecto
                DatosPago.CantidadBoletos = 1;
                CalcularPrecioTotal();

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al cargar datos del evento {EventoId}");
                ModelState.AddModelError(string.Empty, "Error al cargar los datos del evento. Por favor, inténtelo de nuevo.");
                return Page();
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await CargarDatosEventoAsync();
                CalcularPrecioTotal();
                return Page();
            }

            try
            {
                // Verificar si el código de descuento es válido (si se proporcionó)
                decimal descuento = 0;
                if (!string.IsNullOrWhiteSpace(DatosPago.CodigoDescuento))
                {
                    descuento = await VerificarCodigoDescuentoAsync(DatosPago.CodigoDescuento);
                }

                // Calcular el precio total con descuento
                await CargarDatosEventoAsync();
                decimal precioFinal = (PrecioPorBoleto * DatosPago.CantidadBoletos) * (1 - descuento);

                // Guardar la compra en la base de datos
                int idCompra = await GuardarCompraAsync(precioFinal, descuento);

                if (idCompra > 0)
                {
                    // Redirigir a la página de factura
                    return RedirectToPage("/Cliente/Factura", new { FacturaId = idCompra, TipoCompra = "entradas" });
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Error al procesar la compra. Por favor, inténtelo de nuevo.");
                    CalcularPrecioTotal();
                    return Page();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar la compra de entradas");
                ModelState.AddModelError(string.Empty, "Error al procesar la compra. Por favor, inténtelo de nuevo.");
                await CargarDatosEventoAsync();
                CalcularPrecioTotal();
                return Page();
            }
        }

        private async Task CargarDatosEventoAsync()
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                string query = @"
                    SELECT e.nombre, e.fecha, e.lugar, e.hora, e.precio
                    FROM Eventos e
                    WHERE e.id_evento = @EventoId";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@EventoId", EventoId);

                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            string nombre = reader.GetString(reader.GetOrdinal("nombre"));
                            DateTime fecha = reader.GetDateTime(reader.GetOrdinal("fecha"));
                            string lugar = reader.GetString(reader.GetOrdinal("lugar"));
                            string hora = reader.GetString(reader.GetOrdinal("hora"));
                            decimal precio = reader.GetDecimal(reader.GetOrdinal("precio"));

                            ResumenCompra.Evento = nombre;
                            ResumenCompra.TipoBoleto = "General";
                            ResumenCompra.AsientoZona = "Zona General";
                            PrecioPorBoleto = precio;
                        }
                        else
                        {
                            throw new Exception($"No se encontró el evento con ID {EventoId}");
                        }
                    }
                }
            }
        }

        private void CalcularPrecioTotal()
        {
            if (DatosPago.CantidadBoletos <= 0)
            {
                DatosPago.CantidadBoletos = 1;
            }

            PrecioTotal = PrecioPorBoleto * DatosPago.CantidadBoletos;
        }

        private async Task<decimal> VerificarCodigoDescuentoAsync(string codigoDescuento)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                string query = @"
                    SELECT cc.codigo_generado, cp.descuento_valor
                    FROM ComprasCodigos cc
                    INNER JOIN CodigosPromo cp ON cc.id_codigo = cp.id_codigo
                    WHERE cc.codigo_generado = @CodigoDescuento
                    AND cc.usado = 0";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@CodigoDescuento", codigoDescuento);

                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            decimal descuentoValor = reader.GetDecimal(reader.GetOrdinal("descuento_valor"));
                            return descuentoValor;
                        }
                    }
                }

                // Si no se encontró el código o ya fue usado, devolver 0
                return 0;
            }
        }

        private async Task<int> GuardarCompraAsync(decimal precioFinal, decimal descuento)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // Iniciar una transacción para asegurar la integridad de los datos
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Obtener el ID del usuario de la sesión (si está disponible)
                        int? userId = HttpContext.Session.GetInt32("UserId");

                        // Insertar la compra en la tabla Compras
                        string insertQuery = @"
                            INSERT INTO Compras (id_evento, id_usuario, nombre_cliente, correo_electronico, 
                                              fecha, cantidad, total, codigo_descuento, descuento_aplicado)
                            VALUES (@IdEvento, @IdUsuario, @NombreCliente, @CorreoElectronico, 
                                   @Fecha, @Cantidad, @Total, @CodigoDescuento, @DescuentoAplicado);
                            SELECT SCOPE_IDENTITY();";

                        using (SqlCommand command = new SqlCommand(insertQuery, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@IdEvento", EventoId);
                            command.Parameters.AddWithValue("@IdUsuario", userId.HasValue ? (object)userId.Value : DBNull.Value);
                            command.Parameters.AddWithValue("@NombreCliente", DatosPago.NombreCompleto);
                            command.Parameters.AddWithValue("@CorreoElectronico", DatosPago.CorreoElectronico);
                            command.Parameters.AddWithValue("@Fecha", DateTime.Now);
                            command.Parameters.AddWithValue("@Cantidad", DatosPago.CantidadBoletos);
                            command.Parameters.AddWithValue("@Total", precioFinal);
                            command.Parameters.AddWithValue("@CodigoDescuento",
                                string.IsNullOrWhiteSpace(DatosPago.CodigoDescuento) ? DBNull.Value : (object)DatosPago.CodigoDescuento);
                            command.Parameters.AddWithValue("@DescuentoAplicado", descuento);

                            // Obtener el ID de la compra insertada
                            var result = await command.ExecuteScalarAsync();
                            int idCompra = Convert.ToInt32(result);

                            // Si se usó un código de descuento, marcarlo como usado
                            if (!string.IsNullOrWhiteSpace(DatosPago.CodigoDescuento) && descuento > 0)
                            {
                                string updateQuery = @"
                                    UPDATE ComprasCodigos
                                    SET usado = 1, fecha_uso = @FechaUso
                                    WHERE codigo_generado = @CodigoDescuento";

                                using (SqlCommand updateCommand = new SqlCommand(updateQuery, connection, transaction))
                                {
                                    updateCommand.Parameters.AddWithValue("@FechaUso", DateTime.Now);
                                    updateCommand.Parameters.AddWithValue("@CodigoDescuento", DatosPago.CodigoDescuento);
                                    await updateCommand.ExecuteNonQueryAsync();
                                }
                            }

                            // Confirmar la transacción
                            transaction.Commit();

                            return idCompra;
                        }
                    }
                    catch (Exception)
                    {
                        // Revertir la transacción en caso de error
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }
    }

    public class DatosPagoEntradasViewModel
    {
        [Required(ErrorMessage = "El nombre completo es obligatorio")]
        [Display(Name = "Nombre completo")]
        public string NombreCompleto { get; set; } = string.Empty;

        [Required(ErrorMessage = "El correo electrónico es obligatorio")]
        [EmailAddress(ErrorMessage = "El formato del correo electrónico no es válido")]
        [Display(Name = "Correo electrónico")]
        public string CorreoElectronico { get; set; } = string.Empty;

        [Required(ErrorMessage = "El número de tarjeta es obligatorio")]
        [RegularExpression(@"^\d{16}$", ErrorMessage = "El número de tarjeta debe tener 16 dígitos")]
        [Display(Name = "Número de tarjeta")]
        public string NumeroTarjeta { get; set; } = string.Empty;

        [Required(ErrorMessage = "La fecha de vencimiento es obligatoria")]
        [RegularExpression(@"^(0[1-9]|1[0-2])\/\d{2}$", ErrorMessage = "El formato debe ser MM/YY")]
        [Display(Name = "Fecha de vencimiento")]
        public string Vencimiento { get; set; } = string.Empty;

        [Required(ErrorMessage = "El código CVV es obligatorio")]
        [RegularExpression(@"^\d{3,4}$", ErrorMessage = "El CVV debe tener 3 o 4 dígitos")]
        [Display(Name = "CVV")]
        public string CVV { get; set; } = string.Empty;

        [Required(ErrorMessage = "La cantidad de boletos es obligatoria")]
        [Range(1, 10, ErrorMessage = "La cantidad debe estar entre 1 y 10")]
        [Display(Name = "Cantidad de boletos")]
        public int CantidadBoletos { get; set; } = 1;

        [Display(Name = "Código de descuento")]
        public string CodigoDescuento { get; set; } = string.Empty;
    }

    public class ResumenCompraEntradasViewModel
    {
        public string Evento { get; set; } = string.Empty;
        public string TipoBoleto { get; set; } = string.Empty;
        public string AsientoZona { get; set; } = string.Empty;
    }
}