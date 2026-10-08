namespace back.Models;

public sealed class GrupoTrabajo
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string NombreNormalizado { get; set; } = string.Empty;
    public int? SupervisorId { get; set; }
    public Usuario? Supervisor { get; set; }
    public ICollection<MiembroGrupo> Miembros { get; set; } = new List<MiembroGrupo>();
    public ICollection<EstadoTarea> Estados { get; set; } = new List<EstadoTarea>();
    public DateTime CreadoEnUtc { get; set; } = DateTime.UtcNow;
}

public sealed class MiembroGrupo
{
    public int EmpresaId { get; set; }
    public int GrupoId { get; set; }
    public GrupoTrabajo Grupo { get; set; } = null!;
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
}
