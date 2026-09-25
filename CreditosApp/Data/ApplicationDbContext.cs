using CreditosApp.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CreditosApp.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
{
    public DbSet<Cliente> Clientes => Set<Cliente>();

    public DbSet<SolicitudCredito> SolicitudesCredito => Set<SolicitudCredito>();

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
    }
}