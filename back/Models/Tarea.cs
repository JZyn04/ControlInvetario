namespace back.Models;

public sealed class Tarea
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public int GrupoId { get; set; }
    public GrupoTrabajo Grupo { get; set; } = null!;
    public int EstadoId { get; set; }
    public EstadoTarea Estado { get; set; } = null!;
    public string Titulo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public int? AsignadoAId { get; set; }
    public Usuario? AsignadoA { get; set; }
    public bool PermitirAutoasignacion { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreadaEnUtc { get; set; } = DateTime.UtcNow;
    public DateTime ActualizadaEnUtc { get; set; } = DateTime.UtcNow;
}
