using System.Text.Json.Serialization;

namespace back.Models;

public sealed class PrestamoInventario
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public int ProductoId { get; set; }
    public int BodegaOrigenId { get; set; }
    [JsonIgnore] public ProductoInventarioHistorico Producto { get; set; } = null!;
    public decimal Cantidad { get; set; }
    public decimal Devuelta { get; set; }
    public int? DestinatarioUsuarioId { get; set; }
    // Conserva la identificación aunque se elimine la cuenta o cambie su correo.
    public string Destinatario { get; set; } = string.Empty;
    public bool EsExterno { get; set; }
    public DateOnly? FechaPrevista { get; set; }
    public DateTime CreadoEnUtc { get; set; } = DateTime.UtcNow;
    public decimal Pendiente => Cantidad - Devuelta;
}
