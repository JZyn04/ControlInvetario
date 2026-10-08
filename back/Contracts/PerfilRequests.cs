using System.ComponentModel.DataAnnotations;

namespace back.Contracts;

public sealed record ActualizarPerfilRequest(
    [Required, EmailAddress, MaxLength(254)] string Correo,
    [MaxLength(128)] string? Contrasenia,
    [MaxLength(25), RegularExpression(@"^\+?[0-9 ()-]{7,25}$")] string? Telefono,
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
