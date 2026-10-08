using back.Auth;
using back.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace back.Controllers;

[ApiController]
[Route("api/auditoria")]
[Authorize(Policy = Permisos.AdministrarEmpresa)]
public sealed class AuditoriaController(InventoryDbContext database, CurrentUsuario current) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] long? antesDe, [FromQuery] string? entidad,
        [FromQuery] string? entidadId, [FromQuery] string? accion, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        entidad = string.IsNullOrWhiteSpace(entidad) ? null : entidad.Trim();
        entidadId = string.IsNullOrWhiteSpace(entidadId) ? null : entidadId.Trim();
        accion = string.IsNullOrWhiteSpace(accion) ? null : accion.Trim();
        if (entidad is not (null or "Empresa" or "Usuario" or "Rol" or "Producto" or "Grupo" or "Integrante" or "Estado" or "Tarea" or "Nota" or "Movimiento" or "Prestamo" or "Categoria" or "Bodega"))
            return Problem(statusCode: 400, title: "Elegí un tipo de registro válido.");
        if (accion is not (null or "Creado" or "Editado" or "Eliminado"))
            return Problem(statusCode: 400, title: "Elegí una acción válida.");
        if (entidadId is not null && (entidad is null || entidadId.Length > 80))
            return Problem(statusCode: 400, title: "Para buscar un ID, elegí el tipo de registro y usá hasta 80 caracteres.");
        if (antesDe <= 0) return Problem(statusCode: 400, title: "El cursor debe ser positivo.");

        var query = database.Auditoria.AsNoTracking().Where(item => item.EmpresaId == current.EmpresaId);
        if (entidad is not null) query = query.Where(item => item.Entidad == entidad);
        if (entidadId is not null) query = query.Where(item => item.EntidadId == entidadId);
        if (accion is not null) query = query.Where(item => item.Accion == accion);
        if (antesDe is not null) query = query.Where(item => item.Id < antesDe);
        var registros = await query.OrderByDescending(item => item.Id).Take(100).ToListAsync(cancellationToken);
        return Ok(registros);
    }
}
