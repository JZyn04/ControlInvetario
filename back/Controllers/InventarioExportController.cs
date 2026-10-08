using System.Data;
using back.Auth;
using back.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace back.Controllers;

[ApiController]
[Route("api/inventario/exportar")]
[Authorize(Policy = Permisos.VerInventario)]
[Authorize(Policy = Permisos.ExportarInventario)]
public sealed class InventarioExportController(InventoryDbContext database, CurrentUsuario current) : ControllerBase
{
    private const int Limite = 50000;
    private const string Mime = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private static readonly string[] Tipos = ["Inicial", "Entrada", "Venta", "Consumo", "Prestamo", "DevolucionPrestamo", "Devolucion", "Conteo", "Ajuste", "TrasladoSalida", "TrasladoEntrada"];

    [HttpGet("inventario")]
    public async Task<IActionResult> Inventario(CancellationToken token, string estado = "activos", string? buscar = null,
        int? categoriaId = null, int? bodegaId = null)
    {
        if (estado is not ("activos" or "todos" or "inactivos" or "bajos") || buscar?.Length > 120 || categoriaId <= 0 || bodegaId <= 0)
            return Problem(statusCode: 400, title: "Filtros de inventario inválidos.");
        var empresaId = current.EmpresaId;
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, token);
        await database.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", token);
        if (categoriaId is not null && !await database.CategoriasInventario.AnyAsync(c => c.EmpresaId == empresaId && c.Id == categoriaId, token))
            return Problem(statusCode: 400, title: "La categoría no pertenece a tu empresa.");
        if (bodegaId is not null && !await database.BodegasInventario.AnyAsync(b => b.EmpresaId == empresaId && b.Id == bodegaId, token))
            return Problem(statusCode: 400, title: "La bodega no pertenece a tu empresa.");
        var productosQuery = database.Products.AsNoTracking().Where(p => p.EmpresaId == empresaId);
        productosQuery = estado switch
        {
            "activos" => productosQuery.Where(p => p.IsActive),
            "inactivos" => productosQuery.Where(p => !p.IsActive),
            "bajos" => productosQuery.Where(p => p.IsActive && p.Quantity <= p.MinimumStock),
            _ => productosQuery
        };
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var texto = buscar.Trim().ToLowerInvariant();
            productosQuery = productosQuery.Where(p => p.Code.ToLower().Contains(texto) || p.Name.ToLower().Contains(texto));
        }
        if (categoriaId is not null) productosQuery = productosQuery.Where(p => p.CategoriaId == categoriaId);
        if (bodegaId is not null) productosQuery = productosQuery.Where(p => p.BodegaId == bodegaId);
        var productos = await (from producto in productosQuery
                               join bodega in database.BodegasInventario.AsNoTracking().Where(b => b.EmpresaId == empresaId)
                                   on producto.BodegaId equals bodega.Id
                               orderby bodega.Nombre, producto.Code, producto.Id
                               select new { Producto = producto, Bodega = bodega.Nombre })
            .Take(Limite + 1).ToListAsync(token);
        if (productos.Count > Limite) return LimiteExcedido();
        var categorias = await database.CategoriasInventario.AsNoTracking().Where(c => c.EmpresaId == empresaId)
            .Select(c => new { c.Id, c.Nombre }).ToDictionaryAsync(c => c.Id, c => c.Nombre, token);
        var prestados = await (from prestamo in database.PrestamosInventario.AsNoTracking().Where(p => p.EmpresaId == empresaId && p.Devuelta < p.Cantidad)
                              join producto in productosQuery on prestamo.ProductoId equals producto.Id
                              group prestamo by prestamo.ProductoId into grupo
                              select new { Id = grupo.Key, Cantidad = grupo.Sum(p => p.Cantidad - p.Devuelta) })
            .ToDictionaryAsync(p => p.Id, p => p.Cantidad, token);
        await transaction.CommitAsync(token);
        var inventario = productos.Select(r =>
        {
            var p = r.Producto;
            return new object?[] { p.Id, p.Code, p.Name,
                p.CategoriaId is int id ? categorias.GetValueOrDefault(id) : null, p.BodegaId, r.Bodega, p.Unit, p.Quantity,
                prestados.GetValueOrDefault(p.Id), p.MinimumStock, new InventarioExcel.Dinero(p.UnitPrice), p.IsActive ? "Activo" : "Inactivo" };
        }).ToArray();
        var porBodega = productos.Select(r => new object?[] { r.Producto.Id, r.Producto.Code, r.Producto.Name,
            r.Producto.CategoriaId is int id ? categorias.GetValueOrDefault(id) : null, r.Producto.BodegaId, r.Bodega,
            r.Producto.Unit, r.Producto.Quantity, r.Producto.IsActive ? "Activo" : "Inactivo" }).ToArray();
        return File(InventarioExcel.Crear(
            new("Inventario", ["Producto ID", "Código", "Producto", "Categoría", "Bodega ID", "Bodega", "Unidad", "Disponible", "Prestado", "Stock mínimo", "Precio unitario", "Estado"],
                inventario, [16, 20, 35, 25, 16, 25, 18, 18, 18, 18, 20, 16]),
            new("Por bodega", ["Producto ID", "Código", "Producto", "Categoría", "Bodega ID", "Bodega", "Unidad", "Disponible", "Estado"],
                porBodega, [16, 20, 35, 25, 16, 25, 18, 18, 16])), Mime, $"inventario-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx");
    }

    [HttpGet("kardex")]
    public async Task<IActionResult> Kardex(CancellationToken token, int? productoId = null, int? bodegaId = null,
        string? tipo = null, DateOnly? desde = null, DateOnly? hasta = null)
    {
        if (productoId <= 0 || bodegaId <= 0 || (tipo is not null && !Tipos.Contains(tipo, StringComparer.Ordinal)) || desde > hasta)
            return Problem(statusCode: 400, title: "Filtros de kárdex inválidos.");
        var empresaId = current.EmpresaId;
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, token);
        await database.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", token);
        if (productoId is not null && !await database.ProductosHistoricos.AnyAsync(p => p.EmpresaId == empresaId && p.Id == productoId, token))
            return Problem(statusCode: 400, title: "El producto no pertenece a tu empresa.");
        if (bodegaId is not null && !await database.BodegasHistoricas.AnyAsync(b => b.EmpresaId == empresaId && b.Id == bodegaId, token))
            return Problem(statusCode: 400, title: "La bodega no pertenece a tu empresa.");
        var movimientos = database.MovimientosInventario.AsNoTracking().Where(m => m.EmpresaId == empresaId);
        if (productoId is not null) movimientos = movimientos.Where(m => m.ProductoId == productoId);
        if (bodegaId is not null) movimientos = movimientos.Where(m => m.BodegaId == bodegaId);
        if (tipo is not null) movimientos = movimientos.Where(m => m.Tipo == tipo);
        if (desde is not null)
        {
            var inicio = desde.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            movimientos = movimientos.Where(m => m.FechaUtc >= inicio);
        }
        if (hasta is not null && hasta.Value != DateOnly.MaxValue)
        {
            var finExclusivo = hasta.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            movimientos = movimientos.Where(m => m.FechaUtc < finExclusivo);
        }
        var registros = await (from movimiento in movimientos
                               join fichaProducto in database.ProductosHistoricos.AsNoTracking().Where(p => p.EmpresaId == empresaId)
                                   on movimiento.ProductoId equals fichaProducto.Id into productos
                               from producto in productos.DefaultIfEmpty()
                               join fichaBodega in database.BodegasHistoricas.AsNoTracking().Where(b => b.EmpresaId == empresaId)
                                   on movimiento.BodegaId equals fichaBodega.Id into bodegas
                               from bodega in bodegas.DefaultIfEmpty()
                               orderby movimiento.Id
                               select new { Movimiento = movimiento, Producto = producto, Bodega = bodega })
            .Take(Limite + 1).ToListAsync(token);
        if (registros.Count > Limite) return LimiteExcedido();
        await transaction.CommitAsync(token);
        var filas = registros.Select(r =>
        {
            var m = r.Movimiento;
            return new object?[] { m.Id, m.FechaUtc, m.ProductoId, r.Producto?.Code,
                r.Producto?.Name ?? $"Producto #{m.ProductoId}",
                r.Producto is null ? "Sin ficha histórica" : r.Producto.EliminadoEnUtc is not null ? "Eliminado" : "Actual",
                m.BodegaId, r.Bodega?.Nombre ?? $"Bodega #{m.BodegaId}",
                r.Bodega is null ? "Sin ficha histórica" : r.Bodega.EliminadaEnUtc is not null ? "Eliminada" : "Actual",
                m.Tipo, m.Unidad, m.Cantidad,
                m.SaldoAnterior, m.Cambio, m.SaldoPosterior, m.SaldoBodegaAnterior, m.CambioBodega, m.SaldoBodegaPosterior,
                m.Motivo, m.Referencia, m.CorreoAutor, m.AutorId, m.Cliente,
                m.PrecioUnitario is decimal precio ? new InventarioExcel.Dinero(precio) : null,
                m.Total is decimal total ? new InventarioExcel.Dinero(total) : null, m.PrestamoId, m.TrasladoId?.ToString() };
        }).ToArray();
        return File(InventarioExcel.Crear(new InventarioExcel.Hoja("Kárdex",
            ["Movimiento ID", "Fecha UTC", "Producto ID", "Código", "Producto", "Estado producto", "Bodega ID", "Bodega", "Estado bodega", "Tipo", "Unidad", "Cantidad",
                "Saldo anterior (producto)", "Cambio (producto)", "Saldo posterior (producto)",
                "Saldo anterior (bodega)", "Cambio (bodega)", "Saldo posterior (bodega)",
                "Motivo", "Referencia", "Autor", "Autor ID", "Cliente", "Precio unitario", "Total venta", "Préstamo ID", "Traslado ID"],
            filas, [18, 27, 16, 20, 35, 18, 16, 25, 18, 23, 18, 18, 25, 23, 25, 25, 23, 25, 55, 30, 35, 14, 35, 20, 20, 18, 40])),
            Mime, $"kardex-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx");
    }

    private ObjectResult LimiteExcedido() => Problem(statusCode: 400,
        title: "La exportación supera 50.000 filas. Aplicá más filtros y volvé a exportar; no se descargaron datos incompletos.");
}
