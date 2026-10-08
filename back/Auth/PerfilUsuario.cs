using back.Controllers;
using back.Models;
using Microsoft.AspNetCore.Identity;

namespace back.Auth;

public static class PerfilUsuario
{
    public static bool Actualizar(Usuario usuario, string correo, string? telefono, string? contrasenia,
        IPasswordHasher<Usuario> hasher)
    {
        var normalizado = AuthController.NormalizeEmail(correo);
        var cambianCredenciales = normalizado != usuario.CorreoNormalizado || !string.IsNullOrEmpty(contrasenia);
        usuario.Correo = correo.Trim();
        usuario.CorreoNormalizado = normalizado;
        usuario.Telefono = string.IsNullOrWhiteSpace(telefono) ? null : telefono.Trim();
        if (!string.IsNullOrEmpty(contrasenia)) usuario.ContraseniaHash = hasher.HashPassword(usuario, contrasenia);
        return cambianCredenciales;
    }
}
