using System.ComponentModel.DataAnnotations;

namespace back.Models;

public sealed class Product
{
    public int Id { get; set; }

    [Required, MaxLength(40)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int Quantity { get; set; }

    [Range(0, int.MaxValue)]
    public int MinimumStock { get; set; }

    [Range(typeof(decimal), "0", "9999999999")]
    public decimal UnitPrice { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public bool LowStock => Quantity <= MinimumStock;
}
