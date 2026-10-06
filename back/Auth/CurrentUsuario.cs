using System.Security.Claims;

namespace back.Auth;

public sealed class CurrentUsuario(IHttpContextAccessor accessor)
{
    public int EmpresaId => int.Parse(accessor.HttpContext!.User.FindFirstValue("EmpresaId")!);
    public int UsuarioId => int.Parse(accessor.HttpContext!.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
