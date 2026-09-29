using back.Contracts;
using back.Data;
using back.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace back.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController(InventoryDbContext database) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Product>>> GetAll(CancellationToken cancellationToken)
    {
        var products = await database.Products
            .AsNoTracking()
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

        return Ok(products);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Product>> GetById(int id, CancellationToken cancellationToken)
    {
        var product = await database.Products.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost]
    public async Task<ActionResult<Product>> Create(
        SaveProductRequest request,
        CancellationToken cancellationToken)
    {
        var product = new Product();
        Apply(request, product);

        database.Products.Add(product);
        await database.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<Product>> Update(
        int id,
        SaveProductRequest request,
        CancellationToken cancellationToken)
    {
        var product = await database.Products.FindAsync([id], cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        Apply(request, product);
        await database.SaveChangesAsync(cancellationToken);

        return Ok(product);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await database.Products.Where(item => item.Id == id)
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
