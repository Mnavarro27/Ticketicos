using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations; // Para usar [Required]

namespace Tickicos.Pages.Admin
{
    public class EventoDestacado
    {
        [Required]
        public string Id { get; set; } = string.Empty; // Inicialización para evitar NULL

        [Required]
        public string ImagenUrl { get; set; } = string.Empty; // Inicialización para evitar NULL

        [Required]
        public string Nombre { get; set; } = string.Empty; // Inicialización para evitar NULL

        public DateTime Fecha { get; set; }

        [Required]
        public string Hora { get; set; } = string.Empty; // Inicialización para evitar NULL

        [Required]
        public string Lugar { get; set; } = string.Empty; // Inicialización para evitar NULL
    }

    public class AdminEventosDestacadoModel : PageModel
    {
        public List<EventoDestacado> EventosDestacados { get; set; }

        public AdminEventosDestacadoModel()
        {
            // Datos de ejemplo - Reemplazar con datos reales de la base de datos
            EventosDestacados = new List<EventoDestacado>
            {
                new EventoDestacado
                {
                    Id = "1",
                    ImagenUrl = "/images/saprissa.png",
                    Nombre = "Saprissa",
                    Fecha = DateTime.Parse("2024-03-14"),
                    Hora = "20:00",
                    Lugar = "Estadio Ricardo Saprissa"
                },
                new EventoDestacado
                {
                    Id = "2",
                    ImagenUrl = "/images/concierto.png",
                    Nombre = "Concierto Verano",
                    Fecha = DateTime.Parse("2024-07-14"),
                    Hora = "20:00",
                    Lugar = "Estadio Nacional"
                },
                new EventoDestacado
                {
                    Id = "3",
                    ImagenUrl = "/images/verano2024-1.png",
                    Nombre = "Verano 2024",
                    Fecha = DateTime.Parse("2024-08-10"),
                    Hora = "21:00",
                    Lugar = "Teatro nacional"
                },
                new EventoDestacado
                {
                    Id = "4",
                    ImagenUrl = "/images/verano2024-2.png",
                    Nombre = "Verano 2024",
                    Fecha = DateTime.Parse("2024-08-10"),
                    Hora = "20:00",
                    Lugar = "Estadio Nacional"
                },
                new EventoDestacado
                {
                    Id = "5",
                    ImagenUrl = "/images/lucha.png",
                    Nombre = "Lucha Libre",
                    Fecha = DateTime.Parse("2024-02-22"),
                    Hora = "20:00",
                    Lugar = "Estadio Ricardo Saprissa"
                }
            };
        }

        public void OnGet()
        {
            // Aquí iría la lógica para cargar los eventos desde la base de datos
        }

        public IActionResult OnPostEliminarEvento(string id)
        {
            // Aquí iría la lógica para eliminar el evento
            return RedirectToPage();
        }

        public IActionResult OnPostEditarEvento(string id)
        {
            // Aquí iría la lógica para redirigir a la página de edición
            return RedirectToPage("/Admin/EditarEvento", new { id = id });
        }
    }
}
