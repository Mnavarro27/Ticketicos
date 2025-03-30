using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations; // Asegura que puedes usar [Required]

namespace Tickicos.Pages.Admin
{
    public class Rol
    {
        [Required] // Marca la propiedad como obligatoria
        public string Nombre { get; set; } = string.Empty; // Inicializaci�n para evitar null

        public int Usuarios { get; set; }

        [Required] // Marca la propiedad como obligatoria
        public List<string> Permisos { get; set; } = new List<string>(); // Inicializaci�n con lista vac�a
    }

    public class EmpleadoRol
    {
        [Required] // Marca la propiedad como obligatoria
        public string Nombre { get; set; } = string.Empty; // Inicializaci�n para evitar null

        [Required] // Marca la propiedad como obligatoria
        public string RolActual { get; set; } = string.Empty; // Inicializaci�n para evitar null
    }

    public class GestionRolesModel : PageModel
    {
        public List<Rol> Roles { get; set; }

        public List<EmpleadoRol> Empleados { get; set; }

        [BindProperty(SupportsGet = true)]
        public string SearchTerm { get; set; } = string.Empty; // Inicializaci�n para evitar null

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
                    Nombre = "David L�pez",
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
            // Aqu� ir�a la l�gica para eliminar el rol
            return RedirectToPage();
        }

        public IActionResult OnPostEditarRol(string nombre)
        {
            // Aqu� ir�a la l�gica para editar el rol
            return RedirectToPage("/Admin/EditarRol", new { nombre = nombre });
        }

        public IActionResult OnPostCambiarRolEmpleado(string empleado, string nuevoRol)
        {
            // Aqu� ir�a la l�gica para cambiar el rol del empleado
            return RedirectToPage();
        }
    }
}
