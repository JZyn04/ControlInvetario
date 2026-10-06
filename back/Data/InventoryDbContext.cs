using back.Models;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace back.Data;

public sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options)
    : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var product = modelBuilder.Entity<Product>();
        product.ToTable("products");
        product.HasKey(item => item.Id);
        product.HasIndex(item => new { item.EmpresaId, item.Code }).IsUnique();
        product.HasOne<Empresa>().WithMany().HasForeignKey(item => item.EmpresaId)
            .OnDelete(DeleteBehavior.Cascade);
        product.Property(item => item.Code).HasMaxLength(40);
        product.Property(item => item.Name).HasMaxLength(120);
        product.Property(item => item.UnitPrice).HasPrecision(12, 2);

        var empresa = modelBuilder.Entity<Empresa>();
        empresa.ToTable("Empresa");
        empresa.Property(item => item.Nombre).HasMaxLength(140).IsRequired();
        empresa.Property(item => item.Telefono).HasMaxLength(25).IsRequired();
        empresa.Property(item => item.TelefonoOpcional).HasMaxLength(25);

        var usuario = modelBuilder.Entity<Usuario>();
        usuario.ToTable("Usuario");
        usuario.Property(item => item.Correo).HasMaxLength(254).IsRequired();
        usuario.Property(item => item.CorreoNormalizado).HasMaxLength(254).IsRequired();
        usuario.Property(item => item.ContraseniaHash).IsRequired();
        usuario.HasIndex(item => new { item.EmpresaId, item.CorreoNormalizado }).IsUnique();
        usuario.HasOne(item => item.Empresa).WithMany().HasForeignKey(item => item.EmpresaId)
            .OnDelete(DeleteBehavior.Cascade);
        usuario.HasOne(item => item.Rol).WithMany()
            .HasForeignKey(item => new { item.EmpresaId, item.RolId })
            .HasPrincipalKey(item => new { item.EmpresaId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);

        var rol = modelBuilder.Entity<Rol>();
        rol.ToTable("Rol");
        rol.Property(item => item.Nombre).HasMaxLength(80).IsRequired();
        rol.Property(item => item.NombreNormalizado).HasMaxLength(80).IsRequired();
        rol.Property(item => item.CodigoSistema).HasMaxLength(40);
        rol.HasIndex(item => new { item.EmpresaId, item.NombreNormalizado }).IsUnique();
        rol.HasIndex(item => new { item.EmpresaId, item.CodigoSistema }).IsUnique();
        rol.HasOne(item => item.Empresa).WithMany().HasForeignKey(item => item.EmpresaId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<DataProtectionKey>().ToTable("data_protection_keys");
    }
}
