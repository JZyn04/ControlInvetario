using System.Text.Json.Serialization;

namespace back.Models;

// Registro operativo inmutable. El autor se conserva como dato histórico, como en la auditoría.
public sealed class MovimientoInventario
{
    public long Id { get; set; }
    public int EmpresaId { get; set; }
    public int ProductoId { get; set; }
    public int BodegaId { get; set; }
    public Guid? TrasladoId { get; set; }
    public decimal SaldoBodegaAnterior { get; set; }
    public decimal CambioBodega { get; set; }
    public decimal SaldoBodegaPosterior { get; set; }
    public int? PrestamoId { get; set; }
    public string Tipo { get; set; } = "Inicial";
    public string Unidad { get; set; } = "Unidad";
    public decimal Cantidad { get; set; }
    public decimal SaldoAnterior { get; set; }
    public decimal Cambio { get; set; }
    public decimal SaldoPosterior { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public string? Referencia { get; set; }
    public string? Cliente { get; set; }
    public decimal? PrecioUnitario { get; set; }
    public decimal? Total { get; set; }
    public int? AutorId { get; set; }
    public string CorreoAutor { get; set; } = "Sistema";
    public DateTime FechaUtc { get; set; }
    public Guid? SolicitudId { get; set; }
    [JsonIgnore] public string? SolicitudDatos { get; set; }
}
