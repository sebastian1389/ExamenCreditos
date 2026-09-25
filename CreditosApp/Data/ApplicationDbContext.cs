using CreditosApp.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CreditosApp.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
{
    public DbSet<Cliente> Clientes => Set<Cliente>();

    public DbSet<SolicitudCredito> SolicitudesCredito => Set<SolicitudCredito>();

    public DbSet<Notificacion> Notificaciones => Set<Notificacion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.Property(c => c.IngresosMensuales)
                .HasColumnType("decimal(18,2)");

            entity.ToTable(t => t.HasCheckConstraint("CK_Clientes_IngresosMensuales_Positivo", "IngresosMensuales > 0"));

            entity.HasOne(c => c.Usuario)
                .WithMany()
                .HasForeignKey(c => c.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SolicitudCredito>(entity =>
        {
            entity.Property(s => s.MontoSolicitado)
                .HasColumnType("decimal(18,2)");

            entity.ToTable(t => t.HasCheckConstraint("CK_Solicitudes_Monto_Positivo", "MontoSolicitado > 0"));

            entity.Property(s => s.Estado)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.HasOne(s => s.Cliente)
                .WithMany(c => c.Solicitudes)
                .HasForeignKey(s => s.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Notificacion>(entity =>
        {
            entity.Property(n => n.MessageId)
                .HasColumnType("TEXT");

            entity.Property(n => n.UsuarioId)
                .HasMaxLength(450);

            entity.Property(n => n.Texto)
                .HasMaxLength(500);

            entity.HasIndex(n => n.MessageId)
                .IsUnique();

            entity.HasIndex(n => n.UsuarioId);
            entity.HasIndex(n => n.SolicitudId);
        });
    }
}