using System.Security.Claims;
using back.Data;
using back.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace back.Auth;

public sealed class AccesoEmpresa(InventoryDbContext database, IHttpContextAccessor accessor)
{
    private Task<Usuario?>? seleccionada;
    public Task<Usuario?> Seleccionada(ClaimsPrincipal? principal = null)
    {
        if (seleccionada is not null) return seleccionada;
        var user = principal ?? accessor.HttpContext?.User;
        if (!int.TryParse(user?.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId) ||
            !int.TryParse(user?.FindFirstValue("EmpresaId"), out var empresaId))
            return seleccionada = Task.FromResult<Usuario?>(null);
        return seleccionada = database.Usuarios.AsNoTracking().Include(item => item.Rol)
            .SingleOrDefaultAsync(item => item.Id == usuarioId && item.EmpresaId == empresaId);
    }
}

public sealed record PermisoRequirement(string Clave) : IAuthorizationRequirement;

public sealed class PermisoHandler(AccesoEmpresa acceso) : AuthorizationHandler<PermisoRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermisoRequirement requirement)
    {
        var usuario = await acceso.Seleccionada();
        if (usuario is not null && (usuario.Rol.EsAdministrador ||
            (requirement.Clave != Permisos.AdministrarEmpresa && Permisos.DeRol(usuario.Rol).Contains(requirement.Clave))))
            context.Succeed(requirement);
    }
}
