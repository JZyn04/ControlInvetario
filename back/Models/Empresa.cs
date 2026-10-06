namespace back.Models;

public sealed class Empresa
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string? TelefonoOpcional { get; set; }
    public DateTime CreadaEnUtc { get; set; } = DateTime.UtcNow;
}
