namespace back.Models;

// Identidad histórica: permanece al borrar la ficha activa; el ID nunca se reutiliza.
public sealed class ProductoInventarioHistorico
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public int BodegaId { get; set; }
    public int? OrigenCompartidoId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = "Unidad";
    public bool AllowsFractions { get; set; }
    public decimal MinimumStock { get; set; }
    public decimal UnitPrice { get; set; }
    public DateTime? EliminadoEnUtc { get; set; }
}

public sealed class BodegaInventarioHistorica
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public DateTime? EliminadaEnUtc { get; set; }
}
