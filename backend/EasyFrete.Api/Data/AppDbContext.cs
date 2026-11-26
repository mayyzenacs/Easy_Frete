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
    public DbSet<SolicitacaoExclusao> SolicitacoesExclusao => Set<SolicitacaoExclusao>();
    public DbSet<Notificacao> Notificacoes => Set<Notificacao>();
    public DbSet<TokenRedefinicaoSenha> TokensRedefinicaoSenha => Set<TokenRedefinicaoSenha>();
    public DbSet<ControleEnvioRedefinicao> ControlesEnvioRedefinicao => Set<ControleEnvioRedefinicao>();
    public DbSet<Produto> Produtos => Set<Produto>();
    public DbSet<TarifaMagalu> TarifasMagalu => Set<TarifaMagalu>();
    public DbSet<ConfiguracaoMagalu> ConfiguracoesMagalu => Set<ConfiguracaoMagalu>();
    public DbSet<TarifaAmazon> TarifasAmazon => Set<TarifaAmazon>();
    public DbSet<ConfiguracaoAmazon> ConfiguracoesAmazon => Set<ConfiguracaoAmazon>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Usuario>(e =>
        {
            e.ToTable("Usuarios");
            e.HasKey(u => u.Id);
            e.Property(u => u.Id).ValueGeneratedNever();
            e.Property(u => u.Nome).HasMaxLength(120).IsRequired();
            e.Property(u => u.Email).HasMaxLength(180).IsRequired().UseCollation("NOCASE");
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.PasswordHash).IsRequired();
            e.Property(u => u.Role).HasMaxLength(32).IsRequired();
            e.Property(u => u.Status).HasMaxLength(32).IsRequired();
            e.Property(u => u.SecurityStamp).HasMaxLength(64).IsRequired().HasDefaultValue("legacy-stamp");
        });

        modelBuilder.Entity<SolicitacaoExclusao>(e =>
        {
            e.ToTable("SolicitacoesExclusao");
            e.HasKey(s => s.Id);
            e.Property(s => s.Id).ValueGeneratedNever();
            e.Property(s => s.UsuarioAlvoNome).HasMaxLength(120).IsRequired();
            e.Property(s => s.UsuarioAlvoEmail).HasMaxLength(180).IsRequired();
            e.Property(s => s.SolicitadoPorNome).HasMaxLength(120).IsRequired();
            e.Property(s => s.Motivo).HasMaxLength(500).IsRequired();
            e.Property(s => s.Status).HasMaxLength(32).IsRequired();
            e.Property(s => s.DecididoPorNome).HasMaxLength(120);
            e.Property(s => s.ObservacaoDecisao).HasMaxLength(500);
            e.HasIndex(s => new { s.UsuarioAlvoId, s.Status });
            e.HasIndex(s => new { s.SolicitadoPorId, s.CriadoEm });
        });

        modelBuilder.Entity<Notificacao>(e =>
        {
            e.ToTable("Notificacoes");
            e.HasKey(n => n.Id);
            e.Property(n => n.Id).ValueGeneratedNever();
            e.Property(n => n.Tipo).HasMaxLength(48).IsRequired();
            e.Property(n => n.Titulo).HasMaxLength(140).IsRequired();
            e.Property(n => n.Mensagem).HasMaxLength(500).IsRequired();
            e.HasIndex(n => new { n.DestinatarioId, n.Lida, n.CriadoEm });
            e.HasOne(n => n.Destinatario)
                .WithMany()
                .HasForeignKey(n => n.DestinatarioId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TokenRedefinicaoSenha>(e =>
        {
            e.ToTable("TokensRedefinicaoSenha");
            e.HasKey(t => t.Id);
            e.Property(t => t.Id).ValueGeneratedNever();
            e.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();
            e.HasIndex(t => t.TokenHash).IsUnique();
            e.HasIndex(t => new { t.UsuarioId, t.CriadoEm });
            e.HasOne(t => t.Usuario)
                .WithMany()
                .HasForeignKey(t => t.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ControleEnvioRedefinicao>(e =>
        {
            e.ToTable("ControlesEnvioRedefinicao");
            e.HasKey(c => c.EmailHash);
            e.Property(c => c.EmailHash).HasMaxLength(64).IsRequired();
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

        modelBuilder.Entity<ConfiguracaoMagalu>(e =>
        {
            e.ToTable("ConfiguracoesMagalu");
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<TarifaAmazon>(e =>
        {
            e.ToTable("TarifasAmazon");
            e.HasKey(t => t.Id);
            e.Property(t => t.Id).ValueGeneratedNever();
            e.Property(t => t.Modalidade).HasMaxLength(16).IsRequired();
            e.Property(t => t.TipoRegra).HasMaxLength(24).IsRequired();
            e.Property(t => t.DescricaoFaixaPeso).HasMaxLength(100).IsRequired();
            e.Property(t => t.PrecoMin).HasPrecision(10, 2);
            e.Property(t => t.PrecoMax).HasPrecision(10, 2);
            e.Property(t => t.PesoMinKg).HasPrecision(10, 3);
            e.Property(t => t.PesoMaxKg).HasPrecision(10, 3);
            e.Property(t => t.Tarifa).HasPrecision(10, 2);
            e.Property(t => t.FreteCliente).HasMaxLength(32).IsRequired();
            e.HasIndex(t => new { t.Modalidade, t.TipoRegra, t.PrecoMin, t.PrecoMax, t.PesoMaxKg });
        });

        modelBuilder.Entity<ConfiguracaoAmazon>(e =>
        {
            e.ToTable("ConfiguracoesAmazon");
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).ValueGeneratedNever();
            e.Property(c => c.LimitePrecoTabelaPeso).HasPrecision(10, 2);
            e.Property(c => c.LimitePesoTabelaKg).HasPrecision(10, 3);
            e.Property(c => c.RegiaoReferenciaDba).HasMaxLength(64).IsRequired();
            e.Property(c => c.TaxaFixaObrigatoria).HasPrecision(10, 2);
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
