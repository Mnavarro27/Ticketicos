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
    public class PagoCodigosModel : PageModel
    {
        private readonly ILogger<PagoCodigosModel> _logger;
        private readonly IConfiguration _configuration;
        private readonly EmailService _emailService;
        private readonly string _connectionString;

        public PagoCodigosModel(ILogger<PagoCodigosModel> logger, IConfiguration configuration, EmailService emailService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
            _connectionString = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada.");
        }

        [BindProperty(SupportsGet = true)]
        public int CodigoId { get; set; }

        [BindProperty]
        public DatosPagoViewModel DatosPago { get; set; } = new DatosPagoViewModel();

        public ResumenCompraViewModel ResumenCompra { get; set; } = new ResumenCompraViewModel();

        public decimal PrecioTotal { get; set; }

        public bool MostrarMensajeExito { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (CodigoId <= 0)
            {
                return RedirectToPage("/Cliente/CodigosPromo");
            }

            try
            {
                await CargarDatosCodigoAsync();
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al cargar datos del código promocional {CodigoId}");
                ModelState.AddModelError(string.Empty, "Error al cargar los datos del código promocional. Por favor, inténtelo de nuevo.");
                return Page();
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await CargarDatosCodigoAsync();
                return Page();
            }

            try
            {
                // Generar un código promocional único
                string codigoGenerado = GenerarCodigoUnico();

                // Guardar la compra en la base de datos
                int idCompra = await GuardarCompraAsync(codigoGenerado);

                if (idCompra > 0)
                {
                    // Redirigir a la página de factura
                    return RedirectToPage("/Cliente/Factura", new { FacturaId = idCompra, TipoCompra = "codigos" });
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Error al procesar la compra. Por favor, inténtelo de nuevo.");
                    await CargarDatosCodigoAsync();
                    return Page();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar la compra del código promocional");
                ModelState.AddModelError(string.Empty, "Error al procesar la compra. Por favor, inténtelo de nuevo.");
                await CargarDatosCodigoAsync();
                return Page();
            }
        }

        private async Task CargarDatosCodigoAsync()
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                string query = @"
                    SELECT cp.descripcion, cp.descuento, cp.precio, cp.validez
                    FROM CodigosPromo cp
                    WHERE cp.id_codigo = @CodigoId";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@CodigoId", CodigoId);

                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            string descripcion = reader.GetString(reader.GetOrdinal("descripcion"));
                            string descuento = reader.GetString(reader.GetOrdinal("descuento"));
                            decimal precio = reader.GetDecimal(reader.GetOrdinal("precio"));
                            string validez = reader.GetString(reader.GetOrdinal("validez"));

                            ResumenCompra.TipoCodigo = $"{descripcion} - {descuento}";
                            ResumenCompra.ValidoPara = validez;
                            PrecioTotal = precio;
                        }
                        else
                        {
                            throw new Exception($"No se encontró el código promocional con ID {CodigoId}");
                        }
                    }
                }
            }
        }

        private string GenerarCodigoUnico()
        {
            // Generar un código alfanumérico único de 8 caracteres
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            var codigo = new char[8];

            for (int i = 0; i < codigo.Length; i++)
            {
                codigo[i] = chars[random.Next(chars.Length)];
            }

            return new string(codigo);
        }

        private async Task<int> GuardarCompraAsync(string codigoGenerado)
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

                        // Insertar la compra en la tabla ComprasCodigos
                        string insertQuery = @"
                            INSERT INTO ComprasCodigos (id_codigo, id_usuario, nombre_cliente, correo_electronico, 
                                                      fecha, total, codigo_generado)
                            VALUES (@IdCodigo, @IdUsuario, @NombreCliente, @CorreoElectronico, 
                                   @Fecha, @Total, @CodigoGenerado);
                            SELECT SCOPE_IDENTITY();";

                        using (SqlCommand command = new SqlCommand(insertQuery, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@IdCodigo", CodigoId);
                            command.Parameters.AddWithValue("@IdUsuario", userId.HasValue ? (object)userId.Value : DBNull.Value);
                            command.Parameters.AddWithValue("@NombreCliente", DatosPago.NombreCompleto);
                            command.Parameters.AddWithValue("@CorreoElectronico", DatosPago.CorreoElectronico);
                            command.Parameters.AddWithValue("@Fecha", DateTime.Now);
                            command.Parameters.AddWithValue("@Total", PrecioTotal);
                            command.Parameters.AddWithValue("@CodigoGenerado", codigoGenerado);

                            // Obtener el ID de la compra insertada
                            var result = await command.ExecuteScalarAsync();
                            int idCompra = Convert.ToInt32(result);

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

    public class DatosPagoViewModel
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
    }

    public class ResumenCompraViewModel
    {
        public string TipoCodigo { get; set; } = string.Empty;
        public string ValidoPara { get; set; } = string.Empty;
    }
}