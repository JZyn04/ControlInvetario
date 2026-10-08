using System.ComponentModel.DataAnnotations;

namespace back.Contracts;

public sealed record RegistrarEmpresaRequest(
    [Required, MaxLength(140)] string NombreEmpresa,
    [Required, EmailAddress, MaxLength(254)] string Correo,
    [Required, MinLength(8), MaxLength(128)] string Contrasenia,
    [Required, MaxLength(25), RegularExpression(@"^\+?[0-9 ()-]{7,25}$")] string Telefono,
    [MaxLength(25), RegularExpression(@"^\+?[0-9 ()-]{7,25}$")] string? TelefonoOpcional);

public sealed record LoginRequest(
    [Required, EmailAddress, MaxLength(254)] string Correo,
    [Required, MaxLength(128)] string Contrasenia);

public sealed record CrearUsuarioRequest(
    [Required, EmailAddress, MaxLength(254)] string Correo,
    [Required, MinLength(8), MaxLength(128)] string Contrasenia,
    [Range(1, int.MaxValue)] int RolId,
    [MaxLength(25), RegularExpression(@"^\+?[0-9 ()-]{7,25}$")] string? Telefono = null);

public sealed record ActualizarUsuarioRequest(
    [Required, EmailAddress, MaxLength(254)] string Correo,
    [MaxLength(128)] string? Contrasenia,
    [Range(1, int.MaxValue)] int RolId,
    [MaxLength(25), RegularExpression(@"^\+?[0-9 ()-]{7,25}$")] string? Telefono = null,
    [MaxLength(128)] string? ConfirmacionContrasenia = null) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrEmpty(Contrasenia) &&
            (Contrasenia.Length < 8 || string.IsNullOrWhiteSpace(Contrasenia)))
            yield return new ValidationResult("La contraseña debe tener al menos 8 caracteres.", [nameof(Contrasenia)]);
        if ((!string.IsNullOrEmpty(Contrasenia) || !string.IsNullOrEmpty(ConfirmacionContrasenia)) &&
            !string.Equals(Contrasenia, ConfirmacionContrasenia, StringComparison.Ordinal))
            yield return new ValidationResult("Las contraseñas no coinciden.", [nameof(ConfirmacionContrasenia)]);
    }
}

public sealed record SeleccionarEmpresaRequest([Range(1, int.MaxValue)] int EmpresaId);
public sealed record UsuarioResponse(int Id, string Correo, RolResponse Rol, DateTime CreadoEnUtc, string? Telefono)
{
    public static UsuarioResponse From(back.Models.Usuario usuario) => new(usuario.Id, usuario.Correo,
        RolResponse.From(usuario.Rol), usuario.CreadoEnUtc, usuario.Telefono);
}
public sealed record EmpresaResponse(int Id, string Nombre, string Telefono, string? TelefonoOpcional, RolResponse Rol);
public sealed record SesionResponse(UsuarioResponse Usuario, EmpresaResponse? EmpresaActiva, IReadOnlyList<EmpresaResponse> Empresas);
