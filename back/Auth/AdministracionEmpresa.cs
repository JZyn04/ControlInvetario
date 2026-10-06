using back.Data;
using back.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace back.Auth;

public sealed class AdministracionEmpresa(InventoryDbContext database, CurrentUsuario current)
{
    public async Task<IDbContextTransaction?> Begin(CancellationToken cancellationToken)
    {
        var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        // Serializa los cambios de acceso de una empresa y evita perder al último administrador
        // cuando dos administradores intentan quitarse el rol al mismo tiempo.
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"Empresa\" WHERE \"Id\" = {current.EmpresaId} FOR UPDATE", cancellationToken);
        var sigueSiendoAdmin = await database.Usuarios.AsNoTracking().AnyAsync(item =>
            item.Id == current.UsuarioId && item.EmpresaId == current.EmpresaId &&
            item.Rol.CodigoSistema == Rol.Administrador, cancellationToken);
        if (sigueSiendoAdmin) return transaction;
        await transaction.DisposeAsync();
        return null;
    }
}
