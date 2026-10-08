namespace back.Models;

// Los identificadores de autor y entidad son históricos: sobreviven a su eliminación.
public sealed class RegistroAuditoria
{
    public long Id { get; set; }
    public int EmpresaId { get; set; }
    public int? AutorId { get; set; }
    public string CorreoAutor { get; set; } = string.Empty;
    public DateTime FechaUtc { get; set; } = DateTime.UtcNow;
    public string Accion { get; set; } = string.Empty;
    public string Entidad { get; set; } = string.Empty;
    public string EntidadId { get; set; } = string.Empty;
    public string Detalle { get; set; } = string.Empty;
}
