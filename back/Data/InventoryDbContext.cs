using back.Models;
using Microsoft.EntityFrameworkCore;

namespace back.Data;

public sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options)
    : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var product = modelBuilder.Entity<Product>();
        product.ToTable("products");
        product.HasKey(item => item.Id);
        product.HasIndex(item => item.Code).IsUnique();
        product.Property(item => item.Code).HasMaxLength(40);
        product.Property(item => item.Name).HasMaxLength(120);
        product.Property(item => item.UnitPrice).HasPrecision(12, 2);
    }
}
