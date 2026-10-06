using System.ComponentModel.DataAnnotations;

namespace back.Contracts;

public sealed record RegistrarEmpresaRequest(
    [Required, MaxLength(140)] string NombreEmpresa,
    [Required, EmailAddress, MaxLength(254)] string Correo,
    [Required, MinLength(8), MaxLength(128)] string Contrasenia,
    [Required, RegularExpression(@"^\+?[0-9 ()-]{7,25}$")] string Telefono,
    [RegularExpression(@"^\+?[0-9 ()-]{7,25}$")] string? TelefonoOpcional);

public sealed record LoginRequest(
    [Required, EmailAddress, MaxLength(254)] string Correo,
    [Required, MaxLength(128)] string Contrasenia);

public sealed record CrearUsuarioRequest(
    [Required, EmailAddress, MaxLength(254)] string Correo,
    [Required, MinLength(8), MaxLength(128)] string Contrasenia,
    [Range(1, int.MaxValue)] int RolId);

public sealed record SeleccionarEmpresaRequest([Range(1, int.MaxValue)] int EmpresaId);
public sealed record UsuarioResponse(int Id, string Correo, RolResponse Rol, DateTime CreadoEnUtc)
{
    public static UsuarioResponse From(back.Models.Usuario usuario) => new(usuario.Id, usuario.Correo,
        RolResponse.From(usuario.Rol), usuario.CreadoEnUtc);
}
public sealed record EmpresaResponse(int Id, string Nombre, string Telefono, string? TelefonoOpcional, RolResponse Rol);
public sealed record SesionResponse(UsuarioResponse Usuario, EmpresaResponse? EmpresaActiva, IReadOnlyList<EmpresaResponse> Empresas);
