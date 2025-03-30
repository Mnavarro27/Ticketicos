using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;

namespace Tickicos.Pages.Admin
{
    public class GestionClientesModel : PageModel
    {
        private readonly ILogger<GestionClientesModel> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public GestionClientesModel(ILogger<GestionClientesModel> logger, IConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _connectionString = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("La cadena de conexión 'DefaultConnection' no está configurada.");
        }

        public List<UsuarioViewModel> Usuarios { get; set; } = new List<UsuarioViewModel>();

        [TempData]
        public string? MensajeExito { get; set; }

        [TempData]
        public string? MensajeError { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Busqueda { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                // Verificar si el usuario está autenticado y es administrador
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");

                if (!userId.HasValue || userType != "admin")
                {
                    _logger.LogWarning("Intento de acceso no autorizado a la gestión de usuarios");
                    return RedirectToPage("/Cliente/Login");
                }

                await CargarUsuariosAsync();
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar la página de gestión de usuarios");
                MensajeError = "Ocurrió un error al cargar los usuarios.";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostCambiarEstadoAsync(int id, string nuevoEstado)
        {
            try
            {
                // Verificar si el usuario está autenticado y es administrador
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");

                if (!userId.HasValue || userType != "admin")
                {
                    _logger.LogWarning("Intento de acceso no autorizado para cambiar estado de usuario");
                    return RedirectToPage("/Cliente/Login");
                }

                // Validar que el nuevo estado sea válido
                if (nuevoEstado != "activo" && nuevoEstado != "suspendido")
                {
                    MensajeError = "Estado no válido.";
                    return RedirectToPage();
                }

                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    // No permitir cambiar el estado del propio usuario administrador
                    if (id == userId)
                    {
                        MensajeError = "No puedes cambiar tu propio estado.";
                        return RedirectToPage();
                    }

                    string updateQuery = @"
                        UPDATE Usuarios 
                        SET estado = @Estado 
                        WHERE id_usuario = @Id";

                    using (SqlCommand command = new SqlCommand(updateQuery, connection))
                    {
                        command.Parameters.AddWithValue("@Estado", nuevoEstado);
                        command.Parameters.AddWithValue("@Id", id);
                        int rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            // Registrar la acción en la tabla RegistroAcciones
                            await RegistrarAccionAsync(userId.Value, id, "cambio_estado",
                                $"Cambio de estado de usuario a '{nuevoEstado}'");

                            MensajeExito = $"Estado del usuario actualizado a '{nuevoEstado}'.";
                        }
                        else
                        {
                            MensajeError = "No se encontró el usuario.";
                        }
                    }
                }

                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cambiar estado de usuario");
                MensajeError = "Ocurrió un error al cambiar el estado del usuario.";
                return RedirectToPage();
            }
        }

        public async Task<IActionResult> OnPostBuscarAsync()
        {
            try
            {
                // Verificar si el usuario está autenticado y es administrador
                int? userId = HttpContext.Session.GetInt32("UserId");
                string? userType = HttpContext.Session.GetString("UserType");

                if (!userId.HasValue || userType != "admin")
                {
                    _logger.LogWarning("Intento de acceso no autorizado a la búsqueda de usuarios");
                    return RedirectToPage("/Cliente/Login");
                }

                await CargarUsuariosAsync();
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al buscar usuarios");
                MensajeError = "Ocurrió un error al buscar usuarios.";
                return Page();
            }
        }

        private async Task CargarUsuariosAsync()
        {
            Usuarios.Clear();

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                string query = @"
                    SELECT u.id_usuario, u.nombre, u.apellidos, u.correo, u.tipo_usuario, 
                           u.estado, u.ultima_fecha_acceso
                    FROM Usuarios u
                    WHERE (@Busqueda IS NULL OR 
                           u.nombre LIKE '%' + @Busqueda + '%' OR 
                           u.apellidos LIKE '%' + @Busqueda + '%' OR 
                           u.correo LIKE '%' + @Busqueda + '%')
                    ORDER BY u.ultima_fecha_acceso DESC";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Busqueda", Busqueda ?? (object)DBNull.Value);

                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int id = reader.GetInt32(0);
                            string nombre = reader.GetString(1);
                            string apellidos = reader.GetString(2);
                            string correo = reader.GetString(3);
                            string tipoUsuario = reader.GetString(4);
                            string estado = reader.GetString(5);
                            DateTime? ultimoAcceso = reader.IsDBNull(6) ? null : reader.GetDateTime(6);

                            var usuario = new UsuarioViewModel
                            {
                                Id = id,
                                NombreCompleto = $"{nombre} {apellidos}",
                                Correo = correo,
                                TipoUsuario = tipoUsuario,
                                Estado = estado,
                                UltimoAcceso = ultimoAcceso
                            };

                            Usuarios.Add(usuario);
                        }
                    }
                }
            }
        }

        private async Task RegistrarAccionAsync(int idAdmin, int idUsuarioAfectado, string tipoAccion, string detalleAccion)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string query = @"
                        INSERT INTO RegistroAcciones (id_usuario_admin, id_usuario_afectado, tipo_accion, detalle_accion, fecha_accion)
                        VALUES (@IdAdmin, @IdUsuarioAfectado, @TipoAccion, @DetalleAccion, @FechaAccion)";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@IdAdmin", idAdmin);
                        command.Parameters.AddWithValue("@IdUsuarioAfectado", idUsuarioAfectado);
                        command.Parameters.AddWithValue("@TipoAccion", tipoAccion);
                        command.Parameters.AddWithValue("@DetalleAccion", detalleAccion);
                        command.Parameters.AddWithValue("@FechaAccion", DateTime.Now);

                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar acción administrativa");
                // No lanzamos la excepción para que no afecte al flujo principal
            }
        }
    }

    public class UsuarioViewModel
    {
        public int Id { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;
        public string TipoUsuario { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public DateTime? UltimoAcceso { get; set; }
    }
}

