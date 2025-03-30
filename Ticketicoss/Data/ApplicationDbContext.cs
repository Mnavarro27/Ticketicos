using Microsoft.EntityFrameworkCore;
using WebApplication1.Models;

namespace WebApplication1.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Evento> Eventos { get; set; }
        public DbSet<Preventa> Preventas { get; set; }
        public DbSet<Boleto> Boletos { get; set; }
        public DbSet<TarjetaUsuario> TarjetasUsuario { get; set; } // Cambiado a TarjetaBancaria

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configuración de Evento
            modelBuilder.Entity<Evento>().ToTable("Eventos");
            modelBuilder.Entity<Evento>().HasKey(e => e.IdEvento);
            modelBuilder.Entity<Evento>().Property(e => e.IdEvento).HasColumnName("id_evento");
            modelBuilder.Entity<Evento>().Property(e => e.Nombre).HasColumnName("nombre");
            modelBuilder.Entity<Evento>().Property(e => e.Descripcion).HasColumnName("descripcion");
            modelBuilder.Entity<Evento>().Property(e => e.Fecha).HasColumnName("fecha");
            modelBuilder.Entity<Evento>().Property(e => e.Lugar).HasColumnName("lugar");
            modelBuilder.Entity<Evento>().Property(e => e.Estado).HasColumnName("estado");
            modelBuilder.Entity<Evento>().Property(e => e.ImagenUrl).HasColumnName("imagen_url");

            // Configuración de Preventa
            modelBuilder.Entity<Preventa>().ToTable("Preventa");
            modelBuilder.Entity<Preventa>().HasKey(p => p.IdPreventa);
            modelBuilder.Entity<Preventa>().Property(p => p.IdPreventa).HasColumnName("id_preventa");
            modelBuilder.Entity<Preventa>().Property(p => p.IdEvento).HasColumnName("id_evento");
            modelBuilder.Entity<Preventa>().Property(p => p.FechaInicio).HasColumnName("fecha_inicio");
            modelBuilder.Entity<Preventa>().Property(p => p.FechaFin).HasColumnName("fecha_fin");
            modelBuilder.Entity<Preventa>().Property(p => p.Estado).HasColumnName("estado");

            // Relación Evento-Preventa
            modelBuilder.Entity<Preventa>()
                .HasOne(p => p.Evento)
                .WithMany(e => e.Preventa)
                .HasForeignKey(p => p.IdEvento);

            // Configuración de Boleto
            modelBuilder.Entity<Boleto>().ToTable("Boletos");
            modelBuilder.Entity<Boleto>().HasKey(b => b.IdBoleto);
            modelBuilder.Entity<Boleto>().Property(b => b.IdBoleto).HasColumnName("id_boleto");
            modelBuilder.Entity<Boleto>().Property(b => b.IdEvento).HasColumnName("id_evento");
            modelBuilder.Entity<Boleto>().Property(b => b.Categoria).HasColumnName("categoria");
            modelBuilder.Entity<Boleto>().Property(b => b.Precio).HasColumnName("precio");
            modelBuilder.Entity<Boleto>().Property(b => b.CantidadDisponible).HasColumnName("cantidad_disponible");
            modelBuilder.Entity<Boleto>().Property(b => b.LimiteCompraPorUsuario).HasColumnName("limite_compra_por_usuario");
            modelBuilder.Entity<Boleto>().Property(b => b.IdPreventa).HasColumnName("id_preventa");
            modelBuilder.Entity<Boleto>().Property(b => b.PrecioPreventa).HasColumnName("precio_preventa");
            modelBuilder.Entity<Boleto>().Property(b => b.CantidadPreventa).HasColumnName("cantidad_preventa");

            // Relación Boleto-Evento
            modelBuilder.Entity<Boleto>()
                .HasOne(b => b.Evento)
                .WithMany(e => e.Boletos)
                .HasForeignKey(b => b.IdEvento);

            // Relación Boleto-Preventa
            modelBuilder.Entity<Boleto>()
                .HasOne(b => b.Preventa)
                .WithMany()
                .HasForeignKey(b => b.IdPreventa);

            // No necesitamos configuración adicional para TarjetaBancaria
            // ya que usamos atributos de Data Annotations en la clase
        }
    }
}