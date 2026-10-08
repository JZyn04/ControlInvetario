namespace back.Models;

public sealed class CategoriaInventario
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string NombreNormalizado { get; set; } = string.Empty;
    public bool Activa { get; set; } = true;
}

public sealed class BodegaInventario
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string NombreNormalizado { get; set; } = string.Empty;
    public bool Activa { get; set; } = true;
    public bool Predeterminada { get; set; }
}

public sealed class ExistenciaBodega
{
    public int EmpresaId { get; set; }
    public int ProductoId { get; set; }
    public int BodegaId { get; set; }
    public decimal Cantidad { get; set; }
}
