using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using back.Models;

namespace back.Auth;

public static class CredencialesUsuario
{
    private const string TipoClaim = "CuentaVerificada";

    // La cookie protegida conserva una marca, nunca la contraseña ni su hash de almacenamiento.
    // Cambiar las credenciales invalida el acceso previo, incluso en el selector de empresas.
    public static Claim CrearClaim(Usuario usuario) => new(TipoClaim, Marca(usuario));

    public static bool Verificada(ClaimsPrincipal principal, Usuario usuario) =>
        principal.HasClaim(TipoClaim, Marca(usuario));

    private static string Marca(Usuario usuario) => $"{usuario.Id}:" + Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes($"{usuario.CorreoNormalizado}\n{usuario.ContraseniaHash}")));
}
