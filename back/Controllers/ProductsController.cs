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
public sealed class ProductsController(InventoryDbContext database, CurrentUsuario current) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permisos.VerInventario)]
    public async Task<ActionResult<IReadOnlyList<Product>>> GetAll(CancellationToken cancellationToken)
    {
        var products = await database.Products
            .AsNoTracking()
            .Where(item => item.EmpresaId == current.EmpresaId)
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

        return Ok(products);
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = Permisos.VerInventario)]
    public async Task<ActionResult<Product>> GetById(int id, CancellationToken cancellationToken)
    {
        var product = await database.Products.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id && item.EmpresaId == current.EmpresaId, cancellationToken);

        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost]
    [Authorize(Policy = Permisos.CrearInventario)]
    public async Task<ActionResult<Product>> Create(
        SaveProductRequest request,
        CancellationToken cancellationToken)
    {
        var product = new Product { EmpresaId = current.EmpresaId };
        Apply(request, product);

        database.Products.Add(product);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: "23505" })
        {
            return Problem(statusCode: 409, title: "Ya existe un producto con ese código en tu empresa.");
        }

        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permisos.EditarInventario)]
    public async Task<ActionResult<Product>> Update(
        int id,
        SaveProductRequest request,
        CancellationToken cancellationToken)
    {
        var product = await database.Products
            .SingleOrDefaultAsync(item => item.Id == id && item.EmpresaId == current.EmpresaId, cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        Apply(request, product);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: "23505" })
        {
            return Problem(statusCode: 409, title: "Ya existe un producto con ese código en tu empresa.");
        }

        return Ok(product);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permisos.EliminarInventario)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await database.Products.Where(item => item.Id == id && item.EmpresaId == current.EmpresaId)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted == 0 ? NotFound() : NoContent();
    }

    private static void Apply(SaveProductRequest request, Product product)
    {
        product.Code = request.Code.Trim().ToUpperInvariant();
        product.Name = request.Name.Trim();
        product.Quantity = request.Quantity;
        product.MinimumStock = request.MinimumStock;
        product.UnitPrice = request.UnitPrice;
        product.UpdatedAtUtc = DateTime.UtcNow;
    }
}
