using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations; // Asegura que puedes usar [Required]

namespace Tickicos.Pages.Admin
{
    public class Rol
    {
        [Required] // Marca la propiedad como obligatoria
        public string Nombre { get; set; } = string.Empty; // Inicialización para evitar null

        public int Usuarios { get; set; }

        [Required] // Marca la propiedad como obligatoria
        public List<string> Permisos { get; set; } = new List<string>(); // Inicialización con lista vacía
    }

    public class EmpleadoRol
    {
        [Required] // Marca la propiedad como obligatoria
        public string Nombre { get; set; } = string.Empty; // Inicialización para evitar null

        [Required] // Marca la propiedad como obligatoria
        public string RolActual { get; set; } = string.Empty; // Inicialización para evitar null
    }

    public class GestionRolesModel : PageModel
    {
        public List<Rol> Roles { get; set; }

        public List<EmpleadoRol> Empleados { get; set; }

        [BindProperty(SupportsGet = true)]
        public string SearchTerm { get; set; } = string.Empty; // Inicialización para evitar null

        public GestionRolesModel()
        {
            // Datos de ejemplo - Reemplazar con datos reales de la base de datos
            Roles = new List<Rol>
            {
                new Rol
                {
                    Nombre = "Administrador",
                    Usuarios = 5,
                    Permisos = new List<string> { "Todos los permisos" }
                },
                new Rol
                {
                    Nombre = "Gestor de Eventos",
                    Usuarios = 5,
                    Permisos = new List<string> { "Crear eventos", "Editar eventos" }
                },
                new Rol
                {
                    Nombre = "Soporte al Cliente",
                    Usuarios = 2,
                    Permisos = new List<string> { "Ver tickets", "Responder tickets" }
                }
            };

            Empleados = new List<EmpleadoRol>
            {
                new EmpleadoRol
                {
                    Nombre = "Carlos Rodriguez",
                    RolActual = "Administrador"
                },
                new EmpleadoRol
                {
                    Nombre = "Elena Martinez",
                    RolActual = "Gestor de Eventos"
                },
                new EmpleadoRol
                {
                    Nombre = "David López",
                    RolActual = "Soporte al Cliente"
                }
            };
        }

        public void OnGet()
        {
            if (!string.IsNullOrEmpty(SearchTerm))
            {
                Empleados = Empleados.FindAll(e =>
                    e.Nombre.Contains(SearchTerm, System.StringComparison.OrdinalIgnoreCase));
            }
        }

        public IActionResult OnPostEliminarRol(string nombre)
        {
            // Aquí iría la lógica para eliminar el rol
            return RedirectToPage();
        }

        public IActionResult OnPostEditarRol(string nombre)
        {
            // Aquí iría la lógica para editar el rol
            return RedirectToPage("/Admin/EditarRol", new { nombre = nombre });
        }

        public IActionResult OnPostCambiarRolEmpleado(string empleado, string nuevoRol)
        {
            // Aquí iría la lógica para cambiar el rol del empleado
            return RedirectToPage();
        }
    }
}
