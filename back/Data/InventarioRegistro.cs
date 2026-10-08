using System.Text.Json;
using back.Models;
using Microsoft.EntityFrameworkCore;

namespace back.Data;

public static class InventarioRegistro
{
    public const decimal Maximo = 999999999999.999m;
    public static bool CantidadValida(decimal cantidad, bool fracciones) => cantidad >= 0 && cantidad <= Maximo &&
        decimal.Round(cantidad, 3) == cantidad && (fracciones || decimal.Truncate(cantidad) == cantidad);

    public static Task<BodegaInventario?> Bodega(InventoryDbContext database, int empresa, int? id, CancellationToken token) =>
        database.BodegasInventario.AsNoTracking().SingleOrDefaultAsync(b => b.EmpresaId == empresa && b.Activa &&
            id != null && b.Id == id, token);

    public static async Task<decimal> Disponible(InventoryDbContext database, int empresa, int producto, int bodega, CancellationToken token) =>
        await database.ExistenciasBodegas.Where(e => e.EmpresaId == empresa && e.ProductoId == producto && e.BodegaId == bodega)
            .Select(e => (decimal?)e.Cantidad).SingleOrDefaultAsync(token) ?? 0;

    // El contexto es local a la transacción: nunca se filtra a otra conexión del pool.
    public static Task Contexto(InventoryDbContext database, object movimiento, CancellationToken token) =>
        database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config('inventario.movimiento', {JsonSerializer.Serialize(movimiento)}, true)", token);

    public static async Task Prestados(InventoryDbContext database, IEnumerable<Product> productos, int empresa,
        CancellationToken token)
    {
        var saldos = await database.PrestamosInventario.AsNoTracking().Where(p => p.EmpresaId == empresa && p.Devuelta < p.Cantidad)
            .GroupBy(p => p.ProductoId).Select(g => new { Id = g.Key, Cantidad = g.Sum(p => p.Cantidad - p.Devuelta) })
            .ToDictionaryAsync(p => p.Id, p => p.Cantidad, token);
        foreach (var producto in productos) producto.LoanedQuantity = saldos.GetValueOrDefault(producto.Id);
    }
}
