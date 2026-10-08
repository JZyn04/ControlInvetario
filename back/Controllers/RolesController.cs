using back.Auth;
using back.Contracts;
using back.Data;
using back.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace back.Controllers;

[ApiController]
[Route("api/roles")]
[Authorize(Policy = Permisos.AdministrarEmpresa)]
[AutoValidateAntiforgeryToken]
public sealed class RolesController(InventoryDbContext database, CurrentUsuario current, AdministracionEmpresa administracion) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var roles = await database.Roles.AsNoTracking().Where(item => item.EmpresaId == current.EmpresaId)
            .OrderByDescending(item => item.CodigoSistema != null).ThenBy(item => item.Nombre).ToListAsync(cancellationToken);
        return Ok(new { roles = roles.Select(RolResponse.From).ToArray(),
            permisos = Permisos.Catalogo.Select(item => new PermisoResponse(item.Clave, item.Nombre)).ToArray() });
    }

    [HttpPost]
    public Task<ActionResult<RolResponse>> Create(GuardarRolRequest request, CancellationToken cancellationToken) =>
        Save(null, request, cancellationToken);

    [HttpPut("{id:int}")]
    public Task<ActionResult<RolResponse>> Update(int id, GuardarRolRequest request, CancellationToken cancellationToken) =>
        Save(id, request, cancellationToken);

    private async Task<ActionResult<RolResponse>> Save(int? id, GuardarRolRequest request, CancellationToken cancellationToken)
    {
        if (!Permisos.TryParse(request.Permisos, out var permisos, out var tareas, out var grupos))
            return Problem(statusCode: 400, title: "Los permisos no son válidos. También debés permitir ver la sección correspondiente.");
        await using var transaction = await administracion.Begin(cancellationToken);
        if (transaction is null) return Forbid();
        var rol = id is null ? new Rol { EmpresaId = current.EmpresaId } : await database.Roles
            .SingleOrDefaultAsync(item => item.Id == id && item.EmpresaId == current.EmpresaId, cancellationToken);
        if (rol is null) return NotFound();
        if (rol.EsSistema) return Problem(statusCode: 409, title: "Los roles del sistema no se pueden modificar.");
        rol.Nombre = request.Nombre.Trim();
        rol.NombreNormalizado = rol.Nombre.ToUpperInvariant();
        rol.Permisos = permisos;
        rol.PermisosTareas = tareas;
        rol.PermisosGrupos = grupos;
        if (id is null) database.Roles.Add(rol);
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: "23505" })
        { return Problem(statusCode: 409, title: "Ya existe un rol con ese nombre en esta empresa."); }
        await transaction.CommitAsync(cancellationToken);
        return StatusCode(id is null ? 201 : 200, RolResponse.From(rol));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await using var transaction = await administracion.Begin(cancellationToken);
        if (transaction is null) return Forbid();
        var rol = await database.Roles.SingleOrDefaultAsync(item => item.Id == id && item.EmpresaId == current.EmpresaId, cancellationToken);
        if (rol is null) return NotFound();
        if (rol.EsSistema) return Problem(statusCode: 409, title: "Los roles del sistema no se pueden eliminar.");
        if (await database.Usuarios.AnyAsync(item => item.EmpresaId == current.EmpresaId && item.RolId == id, cancellationToken))
            return Problem(statusCode: 409, title: "Este rol tiene usuarios asignados. Cambiá sus roles antes de eliminarlo.");
        database.Roles.Remove(rol);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return NoContent();
    }
}
