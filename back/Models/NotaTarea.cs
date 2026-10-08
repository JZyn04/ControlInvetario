namespace back.Models;

public sealed class NotaTarea
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public int TareaId { get; set; }
    public Tarea Tarea { get; set; } = null!;
    public int? AutorId { get; set; }
    public Usuario? Autor { get; set; }
    public string CorreoAutor { get; set; } = string.Empty;
    public string TipoAutor { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public DateTime CreadaEnUtc { get; set; } = DateTime.UtcNow;
    public DateTime ActualizadaEnUtc { get; set; } = DateTime.UtcNow;
}
