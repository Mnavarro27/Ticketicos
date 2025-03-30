using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WebApplication1.Services;
using System.Text;

namespace WebApplication1.Pages.Cliente
{
    public class FacturaModel : PageModel
    {
        private readonly ILogger<FacturaModel> _logger;
        private readonly IConfiguration _configuration;
        private readonly EmailService _emailService;
        private readonly string _connectionString;

        public FacturaModel(ILogger<FacturaModel> logger, IConfiguration configuration, EmailService emailService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
            _connectionString = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada.");
        }

        [BindProperty(SupportsGet = true)]
        public int? FacturaId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string TipoCompra { get; set; } = string.Empty;

        public string NumeroFactura { get; set; } = string.Empty;
        public string Evento { get; set; } = string.Empty;
        public string FechaHora { get; set; } = string.Empty;
        public string Lugar { get; set; } = string.Empty;
        public int CantidadBoletos { get; set; }
        public string Total { get; set; } = string.Empty;
        public string CorreoElectronico { get; set; } = string.Empty;
        public string CodigoPromo { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync()
        {
            if (!FacturaId.HasValue)
            {
                return RedirectToPage("/Cliente/EventosDestacados");
            }

            try
            {
                // Determinar el tipo de compra si no se especificó
                if (string.IsNullOrEmpty(TipoCompra))
                {
                    TipoCompra = await DeterminarTipoCompraAsync(FacturaId.Value);
                }

                // Cargar los datos de la factura según el tipo de compra
                if (TipoCompra.Equals("entradas", StringComparison.OrdinalIgnoreCase))
                {
                    await CargarDatosFacturaEntradasAsync(FacturaId.Value);
                }
                else if (TipoCompra.Equals("codigos", StringComparison.OrdinalIgnoreCase))
                {
                    await CargarDatosFacturaCodigosAsync(FacturaId.Value);
                }
                else
                {
                    return RedirectToPage("/Cliente/EventosDestacados");
                }

                // Enviar la factura por correo electrónico
                await EnviarFacturaPorCorreoAsync();

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al cargar la factura {FacturaId}");
                return RedirectToPage("/Cliente/EventosDestacados");
            }
        }

        private async Task<string> DeterminarTipoCompraAsync(int facturaId)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // Primero verificamos si es una compra de entradas
                string queryEntradas = @"
                    SELECT COUNT(1) FROM Compras WHERE id_compra = @FacturaId";

                using (SqlCommand command = new SqlCommand(queryEntradas, connection))
                {
                    command.Parameters.AddWithValue("@FacturaId", facturaId);
                    // Corregido: Manejar posible valor nulo
                    var result = await command.ExecuteScalarAsync();
                    int count = result != null ? Convert.ToInt32(result) : 0;

                    if (count > 0)
                    {
                        return "entradas";
                    }
                }

                // Si no es de entradas, verificamos si es de códigos promocionales
                string queryCodigos = @"
                    SELECT COUNT(1) FROM ComprasCodigos WHERE id_compra = @FacturaId";

                using (SqlCommand command = new SqlCommand(queryCodigos, connection))
                {
                    command.Parameters.AddWithValue("@FacturaId", facturaId);
                    // Corregido: Manejar posible valor nulo
                    var result = await command.ExecuteScalarAsync();
                    int count = result != null ? Convert.ToInt32(result) : 0;

                    if (count > 0)
                    {
                        return "codigos";
                    }
                }

                // Si no encontramos ninguno, devolvemos un valor por defecto
                return "desconocido";
            }
        }

        private async Task CargarDatosFacturaEntradasAsync(int facturaId)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                string query = @"
                    SELECT c.id_compra, c.fecha, c.total, c.cantidad, c.correo_electronico,
                           e.nombre AS evento_nombre, e.fecha AS evento_fecha, e.lugar, e.hora
                    FROM Compras c
                    INNER JOIN Eventos e ON c.id_evento = e.id_evento
                    WHERE c.id_compra = @FacturaId";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@FacturaId", facturaId);

                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            NumeroFactura = $"E-{facturaId:D6}";
                            Evento = reader.GetString(reader.GetOrdinal("evento_nombre"));

                            DateTime fechaEvento = reader.GetDateTime(reader.GetOrdinal("evento_fecha"));
                            string horaEvento = reader.GetString(reader.GetOrdinal("hora"));
                            FechaHora = $"{fechaEvento:dd/MM/yyyy} {horaEvento}";

                            Lugar = reader.GetString(reader.GetOrdinal("lugar"));
                            CantidadBoletos = reader.GetInt32(reader.GetOrdinal("cantidad"));
                            Total = $"€{reader.GetDecimal(reader.GetOrdinal("total")):F2}";
                            CorreoElectronico = reader.GetString(reader.GetOrdinal("correo_electronico"));
                        }
                    }
                }
            }
        }

        private async Task CargarDatosFacturaCodigosAsync(int facturaId)
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                string query = @"
                    SELECT cc.id_compra, cc.fecha, cc.total, cc.correo_electronico, cc.codigo_generado,
                           cp.descripcion, cp.descuento
                    FROM ComprasCodigos cc
                    INNER JOIN CodigosPromo cp ON cc.id_codigo = cp.id_codigo
                    WHERE cc.id_compra = @FacturaId";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@FacturaId", facturaId);

                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            NumeroFactura = $"C-{facturaId:D6}";
                            Evento = "Código Promocional";
                            string descripcion = reader.GetString(reader.GetOrdinal("descripcion"));
                            string descuento = reader.GetString(reader.GetOrdinal("descuento"));
                            FechaHora = $"{reader.GetDateTime(reader.GetOrdinal("fecha")):dd/MM/yyyy HH:mm}";
                            Lugar = "N/A";
                            CantidadBoletos = 1;
                            Total = $"€{reader.GetDecimal(reader.GetOrdinal("total")):F2}";
                            CorreoElectronico = reader.GetString(reader.GetOrdinal("correo_electronico"));
                            CodigoPromo = reader.GetString(reader.GetOrdinal("codigo_generado"));
                        }
                    }
                }
            }
        }

        private async Task EnviarFacturaPorCorreoAsync()
        {
            try
            {
                // Generar el HTML de la factura
                string facturaHtml = GenerarHtmlFactura();

                // Enviar la factura por correo
                await _emailService.SendInvoiceAsync(CorreoElectronico, NumeroFactura, facturaHtml);

                // Si es una compra de entradas, enviar también los boletos
                if (TipoCompra.Equals("entradas", StringComparison.OrdinalIgnoreCase))
                {
                    string ticketsHtml = GenerarHtmlBoletos();
                    await _emailService.SendTicketsAsync(CorreoElectronico, Evento, ticketsHtml);
                }
                // Si es una compra de códigos promocionales, enviar el código
                else if (TipoCompra.Equals("codigos", StringComparison.OrdinalIgnoreCase))
                {
                    await _emailService.SendPromoCodeAsync(CorreoElectronico, CodigoPromo);
                }

                _logger.LogInformation($"Factura {NumeroFactura} enviada a {CorreoElectronico}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al enviar la factura {NumeroFactura} por correo a {CorreoElectronico}");
            }
        }

        private string GenerarHtmlFactura()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine("    <meta charset=\"UTF-8\">");
            sb.AppendLine("    <title>Factura - Tickicos</title>");
            sb.AppendLine("    <style>");
            sb.AppendLine("        body { font-family: Arial, sans-serif; }");
            sb.AppendLine("        .container { max-width: 800px; margin: 0 auto; padding: 20px; }");
            sb.AppendLine("        .header { text-align: center; margin-bottom: 30px; }");
            sb.AppendLine("        .logo { background-color: #c1ff00; width: 70px; height: 70px; margin: 0 auto; display: flex; align-items: center; justify-content: center; border-radius: 10px; }");
            sb.AppendLine("        .invoice-title { font-size: 24px; margin: 20px 0; }");
            sb.AppendLine("        .invoice-details { margin-bottom: 30px; }");
            sb.AppendLine("        .invoice-details table { width: 100%; border-collapse: collapse; }");
            sb.AppendLine("        .invoice-details th, .invoice-details td { padding: 10px; text-align: left; border-bottom: 1px solid #ddd; }");
            sb.AppendLine("        .total { font-size: 18px; font-weight: bold; text-align: right; margin-top: 20px; }");
            sb.AppendLine("        .footer { margin-top: 50px; text-align: center; font-size: 12px; color: #666; }");
            sb.AppendLine("    </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("    <div class=\"container\">");
            sb.AppendLine("        <div class=\"header\">");
            sb.AppendLine("            <div class=\"logo\">TICKICOS</div>");
            sb.AppendLine("            <h1 class=\"invoice-title\">Factura</h1>");
            sb.AppendLine("        </div>");

            sb.AppendLine("        <div class=\"invoice-details\">");
            sb.AppendLine("            <table>");
            sb.AppendLine("                <tr>");
            sb.AppendLine("                    <th>Número de Factura:</th>");
            sb.AppendLine($"                    <td>{NumeroFactura}</td>");
            sb.AppendLine("                </tr>");
            sb.AppendLine("                <tr>");
            sb.AppendLine("                    <th>Fecha:</th>");
            sb.AppendLine($"                    <td>{DateTime.Now:dd/MM/yyyy HH:mm}</td>");
            sb.AppendLine("                </tr>");
            sb.AppendLine("                <tr>");
            sb.AppendLine("                    <th>Cliente:</th>");
            sb.AppendLine($"                    <td>{CorreoElectronico}</td>");
            sb.AppendLine("                </tr>");
            sb.AppendLine("            </table>");
            sb.AppendLine("        </div>");

            sb.AppendLine("        <div class=\"invoice-items\">");
            sb.AppendLine("            <table>");
            sb.AppendLine("                <tr>");
            sb.AppendLine("                    <th>Descripción</th>");
            sb.AppendLine("                    <th>Cantidad</th>");
            sb.AppendLine("                    <th>Precio</th>");
            sb.AppendLine("                </tr>");

            if (TipoCompra.Equals("entradas", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine("                <tr>");
                sb.AppendLine($"                    <td>Entradas para {Evento} - {FechaHora} - {Lugar}</td>");
                sb.AppendLine($"                    <td>{CantidadBoletos}</td>");
                sb.AppendLine($"                    <td>{Total}</td>");
                sb.AppendLine("                </tr>");
            }
            else if (TipoCompra.Equals("codigos", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine("                <tr>");
                sb.AppendLine($"                    <td>Código Promocional - {Evento}</td>");
                sb.AppendLine($"                    <td>{CantidadBoletos}</td>");
                sb.AppendLine($"                    <td>{Total}</td>");
                sb.AppendLine("                </tr>");
            }

            sb.AppendLine("            </table>");

            sb.AppendLine("            <div class=\"total\">");
            sb.AppendLine($"                Total: {Total}");
            sb.AppendLine("            </div>");
            sb.AppendLine("        </div>");

            sb.AppendLine("        <div class=\"footer\">");
            sb.AppendLine("            <p>Gracias por tu compra. Para cualquier consulta, contacta con nuestro servicio de atención al cliente.</p>");
            sb.AppendLine("            <p>© 2025 Tickicos - Todos los derechos reservados</p>");
            sb.AppendLine("        </div>");
            sb.AppendLine("    </div>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        private string GenerarHtmlBoletos()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine("    <meta charset=\"UTF-8\">");
            sb.AppendLine("    <title>Tus Entradas - Tickicos</title>");
            sb.AppendLine("    <style>");
            sb.AppendLine("        body { font-family: Arial, sans-serif; }");
            sb.AppendLine("        .container { max-width: 800px; margin: 0 auto; padding: 20px; }");
            sb.AppendLine("        .header { text-align: center; margin-bottom: 30px; }");
            sb.AppendLine("        .logo { background-color: #c1ff00; width: 70px; height: 70px; margin: 0 auto; display: flex; align-items: center; justify-content: center; border-radius: 10px; }");
            sb.AppendLine("        .ticket-title { font-size: 24px; margin: 20px 0; }");
            sb.AppendLine("        .ticket { border: 2px dashed #333; padding: 20px; margin-bottom: 20px; page-break-inside: avoid; }");
            sb.AppendLine("        .ticket-header { border-bottom: 1px solid #ddd; padding-bottom: 10px; margin-bottom: 10px; }");
            sb.AppendLine("        .ticket-event { font-size: 18px; font-weight: bold; }");
            sb.AppendLine("        .ticket-details { display: flex; justify-content: space-between; }");
            sb.AppendLine("        .ticket-info { flex: 1; }");
            sb.AppendLine("        .ticket-qr { width: 100px; height: 100px; background-color: #eee; display: flex; align-items: center; justify-content: center; }");
            sb.AppendLine("        .footer { margin-top: 50px; text-align: center; font-size: 12px; color: #666; }");
            sb.AppendLine("    </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("    <div class=\"container\">");
            sb.AppendLine("        <div class=\"header\">");
            sb.AppendLine("            <div class=\"logo\">TICKICOS</div>");
            sb.AppendLine("            <h1 class=\"ticket-title\">Tus Entradas</h1>");
            sb.AppendLine("        </div>");

            // Generar un boleto para cada entrada comprada
            for (int i = 1; i <= CantidadBoletos; i++)
            {
                string ticketId = $"{NumeroFactura}-{i:D2}";

                sb.AppendLine("        <div class=\"ticket\">");
                sb.AppendLine("            <div class=\"ticket-header\">");
                sb.AppendLine($"                <div class=\"ticket-event\">{Evento}</div>");
                sb.AppendLine($"                <div>Boleto #{i} de {CantidadBoletos}</div>");
                sb.AppendLine("            </div>");
                sb.AppendLine("            <div class=\"ticket-details\">");
                sb.AppendLine("                <div class=\"ticket-info\">");
                sb.AppendLine($"                    <p><strong>Fecha y hora:</strong> {FechaHora}</p>");
                sb.AppendLine($"                    <p><strong>Lugar:</strong> {Lugar}</p>");
                sb.AppendLine($"                    <p><strong>ID de Boleto:</strong> {ticketId}</p>");
                sb.AppendLine("                </div>");
                sb.AppendLine("                <div class=\"ticket-qr\">");
                sb.AppendLine("                    [QR]");
                sb.AppendLine("                </div>");
                sb.AppendLine("            </div>");
                sb.AppendLine("        </div>");
            }

            sb.AppendLine("        <div class=\"footer\">");
            sb.AppendLine("            <p>Presenta este boleto impreso o en tu dispositivo móvil para acceder al evento.</p>");
            sb.AppendLine("            <p>© 2025 Tickicos - Todos los derechos reservados</p>");
            sb.AppendLine("        </div>");
            sb.AppendLine("    </div>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }
    }
}