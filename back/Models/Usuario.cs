namespace back.Models;

public sealed class Usuario
{
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;
    public int RolId { get; set; }
    public Rol Rol { get; set; } = null!;
    public string Correo { get; set; } = string.Empty;
    public string CorreoNormalizado { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string ContraseniaHash { get; set; } = string.Empty;
    // Se conserva para volver a la versión anterior si fuera necesario; la autorización usa Rol.
    public bool EsPrincipal { get; set; }
    public DateTime CreadoEnUtc { get; set; } = DateTime.UtcNow;
}
