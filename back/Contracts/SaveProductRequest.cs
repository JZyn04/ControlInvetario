using System.ComponentModel.DataAnnotations;

namespace back.Contracts;

public sealed record SaveProductRequest(
    [Required, MaxLength(40)] string Code,
    [Required, MaxLength(120)] string Name,
    [Range(0, int.MaxValue)] int Quantity,
    [Range(0, int.MaxValue)] int MinimumStock,
    [Range(typeof(decimal), "0", "9999999999")] decimal UnitPrice);
