using System;
using System.Collections.Generic;

namespace WebApplication1.Models
{
    public class Evento
    {
        public int IdEvento { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public string Lugar { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public string? ImagenUrl { get; set; }

        // Navegación
        public List<Preventa> Preventa { get; set; } = new List<Preventa>();
        public List<Boleto> Boletos { get; set; } = new List<Boleto>();
    }

    public class Preventa
    {
        public int IdPreventa { get; set; }
        public int IdEvento { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public string Estado { get; set; } = string.Empty;

        // Navegación
        public Evento? Evento { get; set; }
    }

    public class Boleto
    {
        public int IdBoleto { get; set; }
        public int IdEvento { get; set; }
        public string Categoria { get; set; } = string.Empty;
        public decimal Precio { get; set; }
        public int CantidadDisponible { get; set; }
        public int LimiteCompraPorUsuario { get; set; }
        public int? IdPreventa { get; set; }
        public decimal? PrecioPreventa { get; set; }
        public int? CantidadPreventa { get; set; }

        // Navegación
        public Evento? Evento { get; set; }
        public Preventa? Preventa { get; set; }
    }

}