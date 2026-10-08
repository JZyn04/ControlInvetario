using back.Auth;
using back.Contracts;
using back.Data;
using back.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace back.Controllers;

[ApiController, Route("api/inventario"), Authorize(Policy = Permisos.VerInventario), AutoValidateAntiforgeryToken]
public sealed class CatalogosInventarioController(InventoryDbContext database, CurrentUsuario current,
    AdministracionEmpresa administracion) : ControllerBase
{
    [HttpGet("catalogos")]
    public async Task<IActionResult> Catalogos(CancellationToken token) => Ok(new
    {
        categorias = await database.CategoriasInventario.AsNoTracking().Where(c => c.EmpresaId == current.EmpresaId).OrderBy(c => c.Nombre).ToListAsync(token),
        bodegas = await database.BodegasInventario.AsNoTracking().Where(b => b.EmpresaId == current.EmpresaId).OrderBy(b => b.Nombre).ToListAsync(token),
        existencias = await database.ExistenciasBodegas.AsNoTracking().Where(e => e.EmpresaId == current.EmpresaId).ToListAsync(token)
    });

    [HttpPost("categorias"), Authorize(Policy = Permisos.CategoriasInventario)]
    public Task<IActionResult> CrearCategoria(CatalogoInventarioRequest request, CancellationToken token) => Categoria(null, request, token);

    [HttpPut("categorias/{id:int}"), Authorize(Policy = Permisos.CategoriasInventario)]
    public Task<IActionResult> EditarCategoria(int id, CatalogoInventarioRequest request, CancellationToken token) => Categoria(id, request, token);

    [HttpDelete("categorias/{id:int}"), Authorize(Policy = Permisos.CategoriasInventario)]
    public async Task<IActionResult> EliminarCategoria(int id, CancellationToken token)
    {
        await using var tx = await administracion.Begin(token, Permisos.CategoriasInventario);
        if (tx is null) return Forbid();
        var item = await database.CategoriasInventario.SingleOrDefaultAsync(c => c.EmpresaId == current.EmpresaId && c.Id == id, token);
        if (item is null) return NotFound();
        if (await database.Products.AnyAsync(p => p.EmpresaId == current.EmpresaId && p.CategoriaId == id, token))
            return Problem(statusCode: 409, title: "Esta categoría tiene productos. Asignales otra categoría antes de eliminarla.");
        database.CategoriasInventario.Remove(item);
        await database.SaveChangesAsync(token); await tx.CommitAsync(token); return NoContent();
    }

    private async Task<IActionResult> Categoria(int? id, CatalogoInventarioRequest request, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre)) return Problem(statusCode: 400, title: "Escribí el nombre de la categoría.");
        await using var tx = await administracion.Begin(token, Permisos.CategoriasInventario);
        if (tx is null) return Forbid();
        var normal = request.Nombre.Trim().ToUpperInvariant();
        var item = id is null ? new CategoriaInventario { EmpresaId = current.EmpresaId } :
            await database.CategoriasInventario.SingleOrDefaultAsync(c => c.EmpresaId == current.EmpresaId && c.Id == id, token);
        if (item is null) return NotFound();
        if (await database.CategoriasInventario.AnyAsync(c => c.EmpresaId == current.EmpresaId && c.NombreNormalizado == normal && c.Id != id, token))
            return Problem(statusCode: 409, title: "Ya existe una categoría con ese nombre.");
        item.Nombre = request.Nombre.Trim(); item.NombreNormalizado = normal; item.Activa = true;
        if (id is null) database.CategoriasInventario.Add(item);
        await database.SaveChangesAsync(token); await tx.CommitAsync(token); return StatusCode(id is null ? 201 : 200, item);
    }

    [HttpPost("bodegas"), Authorize(Policy = Permisos.BodegasInventario)]
    public Task<IActionResult> CrearBodega(CatalogoInventarioRequest request, CancellationToken token) => Bodega(null, request, token);

    [HttpPut("bodegas/{id:int}"), Authorize(Policy = Permisos.BodegasInventario)]
    public Task<IActionResult> EditarBodega(int id, CatalogoInventarioRequest request, CancellationToken token) => Bodega(id, request, token);

    [HttpDelete("bodegas/{id:int}"), Authorize(Policy = Permisos.BodegasInventario)]
    public async Task<IActionResult> EliminarBodega(int id, CancellationToken token)
    {
        await using var tx = await administracion.Begin(token, Permisos.BodegasInventario);
        if (tx is null) return Forbid();
        var item = await database.BodegasInventario.SingleOrDefaultAsync(b => b.EmpresaId == current.EmpresaId && b.Id == id, token);
        if (item is null) return NotFound();
        if (await database.Products.AnyAsync(p => p.EmpresaId == current.EmpresaId && p.BodegaId == id, token))
            return Problem(statusCode: 409, title: "Vaciá la bodega primero: todavía contiene productos.");
        database.BodegasInventario.Remove(item);
        await database.SaveChangesAsync(token); await tx.CommitAsync(token); return NoContent();
    }

    private async Task<IActionResult> Bodega(int? id, CatalogoInventarioRequest request, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre)) return Problem(statusCode: 400, title: "Escribí el nombre de la bodega.");
        await using var tx = await administracion.Begin(token, Permisos.BodegasInventario);
        if (tx is null) return Forbid();
        var normal = request.Nombre.Trim().ToUpperInvariant();
        if (await database.BodegasInventario.AnyAsync(b => b.EmpresaId == current.EmpresaId && b.NombreNormalizado == normal && b.Id != id, token))
            return Problem(statusCode: 409, title: "Ya existe una bodega con ese nombre.");
        var item = id is null ? new BodegaInventario { EmpresaId = current.EmpresaId } :
            await database.BodegasInventario.SingleOrDefaultAsync(b => b.EmpresaId == current.EmpresaId && b.Id == id, token);
        if (item is null) return NotFound();
        item.Nombre = request.Nombre.Trim(); item.NombreNormalizado = normal; item.Activa = true;
        if (id is null) database.BodegasInventario.Add(item);
        await database.SaveChangesAsync(token); await tx.CommitAsync(token); return StatusCode(id is null ? 201 : 200, item);
    }

}
