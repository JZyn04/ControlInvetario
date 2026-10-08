using back.Contracts;
using back.Auth;
using back.Data;
using back.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace back.Controllers;

[ApiController]
[Route("api/products")]
[Authorize(Policy = "EmpresaSeleccionada")]
[AutoValidateAntiforgeryToken]
public sealed class ProductsController(InventoryDbContext database, CurrentUsuario current, AdministracionEmpresa administracion) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permisos.VerInventario)]
    public async Task<ActionResult<IReadOnlyList<Product>>> GetAll(CancellationToken cancellationToken, bool incluirInactivos = false)
    {
        var products = await database.Products
            .AsNoTracking()
            .Where(item => item.EmpresaId == current.EmpresaId && (incluirInactivos || item.IsActive))
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

        await InventarioRegistro.Prestados(database, products, current.EmpresaId, cancellationToken);
        return Ok(products);
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = Permisos.VerInventario)]
    public async Task<ActionResult<Product>> GetById(int id, CancellationToken cancellationToken)
    {
        var product = await database.Products.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id && item.EmpresaId == current.EmpresaId, cancellationToken);

        if (product is null) return NotFound();
        await InventarioRegistro.Prestados(database, [product], current.EmpresaId, cancellationToken);
        return Ok(product);
    }

    [HttpPost]
    [Authorize(Policy = Permisos.CrearInventario)]
    public async Task<ActionResult<Product>> Create(
        SaveProductRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await administracion.Begin(cancellationToken, Permisos.CrearInventario);
        if (transaction is null) return Forbid();
        if (request.Quantity is not null && request.Quantity != 0)
            return Problem(statusCode: 400, title: "Registrá las existencias desde Movimientos.");
        var errorMessage = Validar(request);
        if (errorMessage is not null) return Problem(statusCode: 400, title: errorMessage);
        var bodega = await InventarioRegistro.Bodega(database, current.EmpresaId, request.BodegaId, cancellationToken);
        if (bodega is null) return Problem(statusCode: 400, title: "Elegí una bodega activa de tu empresa.");
        if (!await CategoriaValida(request.CategoriaId, cancellationToken))
            return Problem(statusCode: 400, title: "Elegí una categoría de tu empresa.");
        var product = new Product { EmpresaId = current.EmpresaId, BodegaId = bodega.Id, Quantity = 0 };
        Apply(request, product);
        await InventarioRegistro.Contexto(database, new { Tipo = "Inicial", BodegaId = bodega.Id, Motivo = "Existencia inicial", AutorId = current.UsuarioId }, cancellationToken);
        database.Products.Add(product);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: "23505" })
        {
            return Problem(statusCode: 409, title: "Ya existe un producto con ese código en esta bodega.");
        }

        await transaction.CommitAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permisos.EditarInventario)]
    public async Task<ActionResult<Product>> Update(
        int id,
        SaveProductRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await administracion.Begin(cancellationToken, Permisos.EditarInventario);
        if (transaction is null) return Forbid();
        var product = await database.Products
            .SingleOrDefaultAsync(item => item.Id == id && item.EmpresaId == current.EmpresaId, cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        if (request.Version is not null && request.Version != product.Version)
            return Problem(statusCode: 409, title: "El producto cambió. Actualizá antes de guardar.");
        if (request.Quantity is not null && request.Quantity != product.Quantity)
            return Problem(statusCode: 400, title: "La existencia se cambia desde Movimientos o Conteo físico.");
        var errorMessage = Validar(request);
        if (errorMessage is not null) return Problem(statusCode: 400, title: errorMessage);
        if (request.BodegaId is not null && request.BodegaId != product.BodegaId)
            return Problem(statusCode: 400, title: "La bodega del producto no se cambia editando la ficha. Usá un traslado.");
        if (!await CategoriaValida(request.CategoriaId, cancellationToken))
            return Problem(statusCode: 400, title: "Elegí una categoría de tu empresa.");
        if ((request.Unit.Trim() != product.Unit || request.AllowsFractions != product.AllowsFractions) &&
            await database.MovimientosInventario.AnyAsync(m => m.EmpresaId == current.EmpresaId && m.ProductoId == id &&
                (m.Tipo != "Inicial" || m.SaldoPosterior != 0), cancellationToken))
            return Problem(statusCode: 409, title: "La unidad no se puede cambiar después de registrar existencias o movimientos.");
        Apply(request, product);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: "23505" })
        {
            return Problem(statusCode: 409, title: "Ya existe un producto con ese código en esta bodega.");
        }
        catch (DbUpdateConcurrencyException)
        { return Problem(statusCode: 409, title: "El producto cambió. Actualizá antes de guardar."); }
        await transaction.CommitAsync(cancellationToken);
        await InventarioRegistro.Prestados(database, [product], current.EmpresaId, cancellationToken);
        return Ok(product);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permisos.EliminarInventario)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await using var transaction = await administracion.Begin(cancellationToken, Permisos.EliminarInventario);
        if (transaction is null) return Forbid();
        var product = await database.Products.SingleOrDefaultAsync(item => item.Id == id && item.EmpresaId == current.EmpresaId, cancellationToken);
        if (product is null) return NotFound();
        if (await database.PrestamosInventario.AnyAsync(p => p.EmpresaId == current.EmpresaId && p.ProductoId == id && p.Devuelta < p.Cantidad, cancellationToken))
            return Problem(statusCode: 409, title: "El producto tiene préstamos pendientes. Registrá sus devoluciones primero.");
        database.Products.Remove(product);
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Problem(statusCode: 409, title: "El producto cambió. Actualizá antes de eliminarlo."); }
        await transaction.CommitAsync(cancellationToken);
        return NoContent();
    }

    private static string? Validar(SaveProductRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Unit))
            return "Completá código, nombre y unidad.";
        if (!InventarioRegistro.CantidadValida(request.Quantity ?? 0, request.AllowsFractions) ||
            !InventarioRegistro.CantidadValida(request.MinimumStock, request.AllowsFractions))
            return "La cantidad debe respetar la unidad: entera o hasta 3 decimales.";
        if (decimal.Round(request.UnitPrice, 2) != request.UnitPrice) return "El precio admite hasta 2 decimales.";
        return null;
    }

    private static void Apply(SaveProductRequest request, Product product)
    {
        product.CategoriaId = request.CategoriaId!.Value;
        product.Code = request.Code.Trim().ToUpperInvariant();
        product.Name = request.Name.Trim();
        product.Unit = request.Unit.Trim();
        product.AllowsFractions = request.AllowsFractions;
        product.MinimumStock = request.MinimumStock;
        product.UnitPrice = request.UnitPrice;
        product.UpdatedAtUtc = DateTime.UtcNow;
    }

    private Task<bool> CategoriaValida(int? id, CancellationToken token) => id is null
        ? Task.FromResult(false)
        : database.CategoriasInventario.AnyAsync(c => c.EmpresaId == current.EmpresaId && c.Id == id, token);
}
