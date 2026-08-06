using Facturacion.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Facturacion.Api.Data;

public class FacturacionDbContext : DbContext
{
    public FacturacionDbContext(DbContextOptions<FacturacionDbContext> options) : base(options)
    {
    }

    public DbSet<Charge> Charges => Set<Charge>();
    public DbSet<ChargeAttempt> ChargeAttempts => Set<ChargeAttempt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Charge>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Amount).HasColumnType("decimal(18,2)");
            e.Property(c => c.Currency).HasMaxLength(3);
            e.Property(c => c.Status).HasMaxLength(20);
            e.HasIndex(c => c.Status);

            e.HasMany(c => c.Attempts)
                .WithOne(a => a.Charge!)
                .HasForeignKey(a => a.ChargeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChargeAttempt>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.TriggeredBy).HasMaxLength(40);
        });
    }
}
