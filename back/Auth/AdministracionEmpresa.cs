using back.Data;
using back.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace back.Auth;

public sealed class AdministracionEmpresa(InventoryDbContext database, CurrentUsuario current)
{
    public Usuario? UsuarioActual { get; private set; }
    public async Task<IDbContextTransaction?> Begin(CancellationToken cancellationToken,
        string? permiso = Permisos.AdministrarEmpresa)
    {
        var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        // Serializa los cambios de acceso de una empresa y evita perder al último administrador
        // cuando dos administradores intentan quitarse el rol al mismo tiempo.
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"Empresa\" WHERE \"Id\" = {current.EmpresaId} FOR UPDATE", cancellationToken);
        var usuario = await database.Usuarios.AsNoTracking().Include(item => item.Rol).SingleOrDefaultAsync(item =>
            item.Id == current.UsuarioId && item.EmpresaId == current.EmpresaId, cancellationToken);
        UsuarioActual = usuario;
        if (usuario is not null && (permiso is null || usuario.Rol.EsAdministrador ||
            Permisos.DeRol(usuario.Rol).Contains(permiso))) return transaction;
        await transaction.DisposeAsync();
        return null;
    }
}
