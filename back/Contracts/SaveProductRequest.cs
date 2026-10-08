using System.ComponentModel.DataAnnotations;

namespace back.Contracts;

public sealed record SaveProductRequest(
    [Required, MaxLength(40)] string Code,
    [Required, MaxLength(120)] string Name,
    [Range(typeof(decimal), "0", "999999999999.999")] decimal? Quantity,
    [Range(typeof(decimal), "0", "999999999999.999")] decimal MinimumStock,
    [Range(typeof(decimal), "0", "9999999999")] decimal UnitPrice,
    [Required, MaxLength(30)] string Unit = "Unidad", bool AllowsFractions = false,
    [Range(1, long.MaxValue)] long? Version = null,
    [Required(ErrorMessage = "Elegí una categoría."), Range(1, int.MaxValue)] int? CategoriaId = null,
    [Range(1, int.MaxValue)] int? BodegaId = null);
