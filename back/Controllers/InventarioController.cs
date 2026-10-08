using System.Text.Json;
using back.Auth;
using back.Contracts;
using back.Data;
using back.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace back.Controllers;

[ApiController]
[Route("api/inventario")]
[Authorize(Policy = Permisos.VerInventario)]
[AutoValidateAntiforgeryToken]
public sealed class InventarioController(InventoryDbContext database, CurrentUsuario current,
    AdministracionEmpresa administracion) : ControllerBase
{
    [HttpGet("personas")]
    [Authorize(Policy = Permisos.PrestamosInventario)]
    public async Task<IActionResult> Personas(CancellationToken token) => Ok(await database.Usuarios.AsNoTracking()
        .Where(u => u.EmpresaId == current.EmpresaId).OrderBy(u => u.Correo)
        .Select(u => new { u.Id, u.Correo }).ToListAsync(token));

    [HttpGet("productos/{id:int}/kardex")]
    public async Task<IActionResult> Kardex(int id, CancellationToken token, long? antesDe = null, string? tipo = null, int? bodegaId = null)
    {
        if (antesDe <= 0 || bodegaId <= 0 || (tipo is not null && Permiso(tipo) is null && tipo is not ("Inicial" or "Ajuste" or "TrasladoSalida" or "TrasladoEntrada")))
            return Problem(statusCode: 400, title: "Filtro de movimientos inválido.");
        var producto = await database.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id && p.EmpresaId == current.EmpresaId, token);
        var eliminado = producto is null;
        if (producto is null)
        {
            var historico = await database.ProductosHistoricos.AsNoTracking().SingleOrDefaultAsync(p => p.EmpresaId == current.EmpresaId && p.Id == id && p.EliminadoEnUtc != null, token);
            if (historico is null) return NotFound();
            producto = new Product { Id = historico.Id, EmpresaId = historico.EmpresaId, BodegaId = historico.BodegaId,
                Code = historico.Code, Name = historico.Name, Unit = historico.Unit, AllowsFractions = historico.AllowsFractions,
                MinimumStock = historico.MinimumStock, UnitPrice = historico.UnitPrice, IsActive = false };
        }
        await InventarioRegistro.Prestados(database, [producto], current.EmpresaId, token);
        var query = database.MovimientosInventario.AsNoTracking().Where(m => m.EmpresaId == current.EmpresaId && m.ProductoId == id);
        if (antesDe is not null) query = query.Where(m => m.Id < antesDe);
        if (!string.IsNullOrEmpty(tipo)) query = query.Where(m => m.Tipo == tipo);
        if (bodegaId is not null) query = query.Where(m => m.BodegaId == bodegaId);
        return Ok(new { producto, eliminado, movimientos = await query.OrderByDescending(m => m.Id).Take(100).ToListAsync(token),
            existencias = await database.ExistenciasBodegas.AsNoTracking().Where(e => e.EmpresaId == current.EmpresaId && e.ProductoId == id).ToListAsync(token),
            bodegas = await database.BodegasHistoricas.AsNoTracking().Where(b => b.EmpresaId == current.EmpresaId).OrderBy(b => b.Nombre)
                .Select(b => new { b.Id, b.Nombre, Activa = b.EliminadaEnUtc == null }).ToListAsync(token) });
    }

    [HttpGet("productos/eliminados")]
    public async Task<IActionResult> Eliminados(CancellationToken token, int? bodegaId = null, int? antesDe = null)
    {
        if (bodegaId <= 0 || antesDe <= 0) return Problem(statusCode: 400, title: "Filtro inválido.");
        var query = database.ProductosHistoricos.AsNoTracking().Where(p => p.EmpresaId == current.EmpresaId && p.EliminadoEnUtc != null);
        if (bodegaId is not null) query = query.Where(p => p.BodegaId == bodegaId);
        if (antesDe is not null) query = query.Where(p => p.Id < antesDe);
        return Ok(await (from p in query join b in database.BodegasHistoricas on new { p.EmpresaId, Id = p.BodegaId } equals new { b.EmpresaId, b.Id }
            orderby p.Id descending select new { p.Id, p.Code, p.Name, p.BodegaId, Bodega = b.Nombre, p.EliminadoEnUtc }).Take(100).ToListAsync(token));
    }

    [HttpGet("prestamos")]
    public async Task<IActionResult> Prestamos(CancellationToken token, int? productoId = null, bool pendientes = true, int pagina = 1)
    {
        if (pagina < 1 || pagina > 100000) return Problem(statusCode: 400, title: "Página inválida.");
        var query = database.PrestamosInventario.AsNoTracking().Where(p => p.EmpresaId == current.EmpresaId);
        if (productoId is not null) query = query.Where(p => p.ProductoId == productoId);
        if (pendientes) query = query.Where(p => p.Devuelta < p.Cantidad);
        return Ok(await query.OrderByDescending(p => p.Id).Skip((pagina - 1) * 100).Take(100)
            .Select(p => new { p.Id, p.ProductoId, p.BodegaOrigenId, Producto = p.Producto.Name, Unidad = p.Producto.Unit,
                p.Cantidad, p.Devuelta, Pendiente = p.Cantidad - p.Devuelta, p.DestinatarioUsuarioId,
                p.Destinatario, p.EsExterno, p.FechaPrevista, p.CreadoEnUtc }).ToListAsync(token));
    }

    [HttpPost("movimientos")]
    public async Task<IActionResult> Registrar(MovimientoInventarioRequest request, CancellationToken token)
    {
        var permiso = Permiso(request.Tipo);
        if (permiso is null || request.SolicitudId == Guid.Empty || string.IsNullOrWhiteSpace(request.Motivo))
            return Problem(statusCode: 400, title: "Elegí un movimiento y escribí el motivo.");
        await using var transaction = await administracion.Begin(token, permiso);
        if (transaction is null) return Forbid();
        var datos = JsonSerializer.Serialize(request);
        var anterior = await database.MovimientosInventario.AsNoTracking().SingleOrDefaultAsync(m =>
            m.EmpresaId == current.EmpresaId && m.SolicitudId == request.SolicitudId, token);
        if (anterior is not null)
        {
            if (anterior.AutorId != current.UsuarioId || anterior.SolicitudDatos != datos)
                return Problem(statusCode: 409, title: "La solicitud ya fue usada para otro movimiento.");
            return Ok(anterior); // Un reintento no vuelve a sumar o restar existencias.
        }
        var producto = await database.Products.SingleOrDefaultAsync(p => p.Id == request.ProductoId && p.EmpresaId == current.EmpresaId, token);
        if (producto is null) return NotFound();
        if (!producto.IsActive) return Problem(statusCode: 409, title: "Activá el producto antes de registrar movimientos.");
        if (producto.Version != request.Version) return Problem(statusCode: 409, title: "La existencia cambió. Actualizá y revisá la cantidad antes de confirmar.");
        var bodega = await InventarioRegistro.Bodega(database, current.EmpresaId, request.BodegaId, token);
        if (bodega is null) return Problem(statusCode: 400, title: "Elegí una bodega activa de tu empresa.");
        if (producto.BodegaId != bodega.Id) return Problem(statusCode: 400, title: "El movimiento debe usar la bodega de este producto.");
        var disponible = await InventarioRegistro.Disponible(database, current.EmpresaId, producto.Id, bodega.Id, token);
        if (!InventarioRegistro.CantidadValida(request.Cantidad, producto.AllowsFractions) ||
            (request.Cantidad == 0 && request.Tipo != "Conteo"))
            return Problem(statusCode: 400, title: "La cantidad debe ser positiva y respetar la unidad elegida (hasta 3 decimales).");
        var esPrestamo = request.Tipo == "Prestamo";
        var esDevolucionPrestamo = request.Tipo == "DevolucionPrestamo";
        if ((request.Tipo != "Venta" && (request.VentaDetallada || request.Cliente is not null || request.PrecioUnitario is not null)) ||
            (!esPrestamo && (request.DestinatarioUsuarioId is not null || request.DestinatarioExterno is not null || request.FechaPrevista is not null)) ||
            (!esDevolucionPrestamo && request.PrestamoId is not null))
            return Problem(statusCode: 400, title: "Los datos adicionales no corresponden al tipo de movimiento.");
        if (request.Tipo == "Venta" && (request.VentaDetallada
            ? string.IsNullOrWhiteSpace(request.Cliente) || request.PrecioUnitario is null || decimal.Round(request.PrecioUnitario.Value, 2) != request.PrecioUnitario
            : request.Cliente is not null || request.PrecioUnitario is not null))
            return Problem(statusCode: 400, title: "Una venta detallada requiere cliente y precio con hasta 2 decimales.");

        var salida = request.Tipo is "Venta" or "Consumo" or "Prestamo";
        var cambio = request.Tipo == "Conteo" ? request.Cantidad - disponible : salida ? -request.Cantidad : request.Cantidad;
        var saldo = producto.Quantity + cambio;
        if (disponible + cambio < 0 || disponible + cambio > InventarioRegistro.Maximo || saldo < 0 || saldo > InventarioRegistro.Maximo)
            return Problem(statusCode: 409, title: "La salida supera la existencia disponible o la entrada excede el límite de cantidad.");
        PrestamoInventario? prestamo = null;
        if (esPrestamo)
        {
            var externo = string.IsNullOrWhiteSpace(request.DestinatarioExterno) ? null : request.DestinatarioExterno.Trim();
            if ((request.DestinatarioUsuarioId is null) == (externo is null))
                return Problem(statusCode: 400, title: "Elegí un usuario de la empresa o escribí un destinatario externo, no ambos.");
            string? nombre = externo;
            if (request.DestinatarioUsuarioId is not null)
                nombre = await database.Usuarios.Where(u => u.Id == request.DestinatarioUsuarioId && u.EmpresaId == current.EmpresaId)
                    .Select(u => u.Correo).SingleOrDefaultAsync(token);
            if (nombre is null) return Problem(statusCode: 400, title: "El destinatario no pertenece a tu empresa.");
            prestamo = new PrestamoInventario { EmpresaId = current.EmpresaId, ProductoId = producto.Id, Cantidad = request.Cantidad,
                BodegaOrigenId = bodega.Id,
                DestinatarioUsuarioId = request.DestinatarioUsuarioId, Destinatario = nombre, EsExterno = externo is not null, FechaPrevista = request.FechaPrevista };
            database.PrestamosInventario.Add(prestamo);
            await database.SaveChangesAsync(token);
        }
        if (esDevolucionPrestamo)
        {
            prestamo = await database.PrestamosInventario.SingleOrDefaultAsync(p => p.Id == request.PrestamoId &&
                p.EmpresaId == current.EmpresaId && p.ProductoId == producto.Id, token);
            if (prestamo is null) return Problem(statusCode: 400, title: "Elegí un préstamo de este producto y empresa.");
            if (request.Cantidad > prestamo.Pendiente) return Problem(statusCode: 409, title: "La devolución supera la cantidad pendiente del préstamo.");
            prestamo.Devuelta += request.Cantidad;
        }
        await InventarioRegistro.Contexto(database, new
        {
            request.Tipo, request.Cantidad, BodegaId = bodega.Id, Motivo = request.Motivo.Trim(), Referencia = request.Referencia?.Trim(),
            request.SolicitudId, SolicitudDatos = datos, AutorId = current.UsuarioId, PrestamoId = prestamo?.Id,
            Cliente = request.VentaDetallada ? request.Cliente!.Trim() : null,
            PrecioUnitario = request.VentaDetallada ? request.PrecioUnitario : null
        }, token);
        producto.Quantity = saldo;
        producto.UpdatedAtUtc = DateTime.UtcNow; // Un conteo sin diferencia también tiene un registro.
        try { await database.SaveChangesAsync(token); }
        catch (DbUpdateConcurrencyException)
        { return Problem(statusCode: 409, title: "La existencia cambió. Actualizá antes de confirmar."); }
        var movimiento = await database.MovimientosInventario.AsNoTracking().SingleAsync(m =>
            m.EmpresaId == current.EmpresaId && m.SolicitudId == request.SolicitudId, token);
        await transaction.CommitAsync(token);
        return StatusCode(201, movimiento);
    }

    [HttpPost("traslados"), Authorize(Policy = Permisos.TrasladosInventario)]
    public async Task<IActionResult> Trasladar(TrasladoInventarioRequest request, CancellationToken token)
    {
        if (request.SolicitudId == Guid.Empty || request.OrigenId == request.DestinoId || string.IsNullOrWhiteSpace(request.Motivo))
            return Problem(statusCode: 400, title: "Elegí bodegas distintas y escribí el motivo del traslado.");
        await using var tx = await administracion.Begin(token, Permisos.TrasladosInventario);
        if (tx is null) return Forbid();
        var datos = JsonSerializer.Serialize(request);
        var anterior = await database.MovimientosInventario.AsNoTracking().SingleOrDefaultAsync(m => m.EmpresaId == current.EmpresaId && m.SolicitudId == request.SolicitudId, token);
        if (anterior is not null)
        {
            if (anterior.AutorId != current.UsuarioId || anterior.SolicitudDatos != datos)
                return Problem(statusCode: 409, title: "La solicitud ya fue usada para otra operación.");
            return Ok(await database.MovimientosInventario.AsNoTracking().Where(m => m.EmpresaId == current.EmpresaId && m.TrasladoId == request.SolicitudId).OrderBy(m => m.Id).ToListAsync(token));
        }
        var producto = await database.Products.SingleOrDefaultAsync(p => p.EmpresaId == current.EmpresaId && p.Id == request.ProductoId, token);
        if (producto is null) return NotFound();
        if (!producto.IsActive || producto.Version != request.Version)
            return Problem(statusCode: 409, title: "El producto está inactivo o cambió. Actualizá antes de trasladar.");
        if (request.Cantidad == 0 || !InventarioRegistro.CantidadValida(request.Cantidad, producto.AllowsFractions))
            return Problem(statusCode: 400, title: "La cantidad debe ser positiva y respetar la unidad del producto.");
        var origen = await InventarioRegistro.Bodega(database, current.EmpresaId, request.OrigenId, token);
        var destino = await InventarioRegistro.Bodega(database, current.EmpresaId, request.DestinoId, token);
        if (origen is null || destino is null) return Problem(statusCode: 400, title: "Elegí bodegas activas de tu empresa.");
        if (producto.BodegaId != origen.Id) return Problem(statusCode: 400, title: "El producto de origen pertenece a otra bodega.");
        var receptor = await database.Products.SingleOrDefaultAsync(p => p.EmpresaId == current.EmpresaId && p.Id == request.ProductoDestinoId && p.BodegaId == destino.Id, token);
        if (receptor is null) return Problem(statusCode: 400, title: "Elegí un producto de la bodega de destino.");
        if (!receptor.IsActive || receptor.Unit != producto.Unit || receptor.AllowsFractions != producto.AllowsFractions)
            return Problem(statusCode: 400, title: "Los productos deben usar la misma unidad y admitir el mismo tipo de cantidad.");
        if (receptor.Version != request.VersionDestino)
            return Problem(statusCode: 409, title: "El producto de destino cambió. Actualizá antes de trasladar.");
        if (receptor.Quantity + request.Cantidad > InventarioRegistro.Maximo)
            return Problem(statusCode: 409, title: "El traslado supera el límite del producto de destino.");
        var disponible = await InventarioRegistro.Disponible(database, current.EmpresaId, producto.Id, origen.Id, token);
        if (disponible < request.Cantidad) return Problem(statusCode: 409, title: "La bodega de origen no tiene esa existencia disponible.");
        try
        {
            foreach (var (tipo, id, ficha, cambio) in new[] { ("TrasladoSalida", origen.Id, producto, -request.Cantidad), ("TrasladoEntrada", destino.Id, receptor, request.Cantidad) })
            {
                await InventarioRegistro.Contexto(database, new { Tipo = tipo, BodegaId = id, request.Cantidad, Motivo = request.Motivo.Trim(),
                    Referencia = request.Referencia?.Trim(), TrasladoId = request.SolicitudId,
                    SolicitudId = tipo == "TrasladoSalida" ? (Guid?)request.SolicitudId : null,
                    SolicitudDatos = datos, AutorId = current.UsuarioId }, token);
                ficha.Quantity += cambio;
                ficha.UpdatedAtUtc = DateTime.UtcNow;
                database.Entry(ficha).Property(p => p.UpdatedAtUtc).IsModified = true;
                await database.SaveChangesAsync(token);
            }
        }
        catch (DbUpdateConcurrencyException) { return Problem(statusCode: 409, title: "La existencia cambió. Actualizá antes de trasladar."); }
        var registros = await database.MovimientosInventario.AsNoTracking().Where(m => m.EmpresaId == current.EmpresaId && m.TrasladoId == request.SolicitudId).OrderBy(m => m.Id).ToListAsync(token);
        await tx.CommitAsync(token); return StatusCode(201, registros);
    }

    private static string? Permiso(string tipo) => tipo switch
    {
        "Entrada" or "Devolucion" => Permisos.EntradasInventario,
        "Venta" or "Consumo" => Permisos.SalidasInventario,
        "Prestamo" or "DevolucionPrestamo" => Permisos.PrestamosInventario,
        "Conteo" => Permisos.ConteosInventario,
        _ => null
    };
}
