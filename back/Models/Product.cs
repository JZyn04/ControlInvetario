using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace back.Models;

public sealed class Product
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public int BodegaId { get; set; }
    public int CategoriaId { get; set; }

    [Required, MaxLength(40)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [Range(typeof(decimal), "0", "999999999999.999")]
    public decimal Quantity { get; set; }

    [Range(typeof(decimal), "0", "999999999999.999")]
    public decimal MinimumStock { get; set; }

    [Required, MaxLength(30)]
    public string Unit { get; set; } = "Unidad";
    public bool AllowsFractions { get; set; }
    public bool IsActive { get; set; } = true;
    public long Version { get; set; }
    [NotMapped] public decimal LoanedQuantity { get; set; }

    [Range(typeof(decimal), "0", "9999999999")]
    public decimal UnitPrice { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public bool LowStock => Quantity <= MinimumStock;
}
