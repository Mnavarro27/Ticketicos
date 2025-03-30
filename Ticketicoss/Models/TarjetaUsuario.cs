using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models
{
    [Table("TarjetasUsuario")]
    public class TarjetaUsuario
    {
        [Key]
        [Column("IdTarjeta")]
        public int IdTarjeta { get; set; }

        [Column("IdUsuario")]
        public int IdUsuario { get; set; }

        [Required]
        [Column("TipoTarjeta")]
        [StringLength(50)]
        public string TipoTarjeta { get; set; } = string.Empty;

        [Required]
        [Column("NumeroTarjeta")]
        [StringLength(20)]
        public string NumeroTarjeta { get; set; } = string.Empty;

        [Required]
        [Column("Vencimiento")]
        [StringLength(7)]
        public string Vencimiento { get; set; } = string.Empty;

        [Required]
        [Column("CVV")]
        [StringLength(4)]
        public string CVV { get; set; } = string.Empty;
    }
}