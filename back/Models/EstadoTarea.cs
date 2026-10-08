namespace back.Models;

public sealed class EstadoTarea
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public int GrupoId { get; set; }
    public GrupoTrabajo Grupo { get; set; } = null!;
    public string Nombre { get; set; } = string.Empty;
    public string NombreNormalizado { get; set; } = string.Empty;
    public string Color { get; set; } = "#6c757d";
    public int Orden { get; set; }
    public bool EsPredeterminado { get; set; }
}
