using EasyFrete.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EasyFrete.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Produto> Produtos => Set<Produto>();
    public DbSet<TarifaMagalu> TarifasMagalu => Set<TarifaMagalu>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Usuario>(e =>
        {
            e.ToTable("Usuarios");
            e.HasKey(u => u.Id);
            e.Property(u => u.Nome).HasMaxLength(120).IsRequired();
            e.Property(u => u.Email).HasMaxLength(180).IsRequired().UseCollation("NOCASE");
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.SenhaHash).IsRequired();
        });

        modelBuilder.Entity<Produto>(e =>
        {
            e.ToTable("Produtos");
            e.HasKey(p => p.Sku);
            e.Property(p => p.Sku).HasMaxLength(64).UseCollation("NOCASE");
            e.Property(p => p.Nome).HasMaxLength(255).IsRequired();
        });

        modelBuilder.Entity<TarifaMagalu>(e =>
        {
            e.ToTable("TarifasMagalu");
            e.HasKey(t => t.Id);
            e.Property(t => t.Id).ValueGeneratedNever();
            e.Property(t => t.DescricaoFaixa).HasMaxLength(100).IsRequired();
        });
    }
}

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=easyfrete.db")
            .Options;

        return new AppDbContext(options);
    }
}
