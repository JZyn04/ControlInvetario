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
[Route("api/grupos")]
[Authorize(Policy = "EmpresaSeleccionada")]
[AutoValidateAntiforgeryToken]
public sealed class GruposController(InventoryDbContext database, CurrentUsuario current,
    AccesoEmpresa acceso, AdministracionEmpresa administracion) : ControllerBase
{
    private IQueryable<GrupoTrabajo> Query() => database.Grupos
        .Include(item => item.Supervisor).ThenInclude(item => item!.Rol)
        .Include(item => item.Miembros).ThenInclude(item => item.Usuario).ThenInclude(item => item.Rol)
        .Include(item => item.Estados).AsSplitQuery().Where(item => item.EmpresaId == current.EmpresaId);

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var usuario = (await acceso.Seleccionada())!;
        if (!AccesoTrabajo.Tiene(usuario, Permisos.VerGrupos) && !AccesoTrabajo.Tiene(usuario, Permisos.VerTareas)) return Forbid();
        var grupos = await Query().AsNoTracking().OrderBy(item => item.Nombre).ToListAsync(cancellationToken);
        return Ok(grupos.Where(item => AccesoTrabajo.Ve(usuario, item)).Select(item => GrupoResponse.From(item, usuario)).ToArray());
    }

    [HttpGet("personas")]
    [Authorize(Policy = Permisos.GestionarGrupos)]
    public async Task<IActionResult> People(CancellationToken cancellationToken) => Ok((await database.Usuarios
        .AsNoTracking().Include(item => item.Rol).Where(item => item.EmpresaId == current.EmpresaId)
        .OrderBy(item => item.Correo).ToListAsync(cancellationToken)).Select(PersonaTrabajoResponse.From).ToArray());

    [HttpPost]
    [Authorize(Policy = Permisos.GestionarGrupos)]
    public Task<IActionResult> Create(GuardarGrupoRequest request, CancellationToken cancellationToken) => Save(null, request, cancellationToken);

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permisos.GestionarGrupos)]
    public Task<IActionResult> Update(int id, GuardarGrupoRequest request, CancellationToken cancellationToken) => Save(id, request, cancellationToken);

    private async Task<IActionResult> Save(int? id, GuardarGrupoRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await administracion.Begin(cancellationToken, Permisos.GestionarGrupos);
        if (transaction is null) return Forbid();
        var grupo = id is null ? new GrupoTrabajo { EmpresaId = current.EmpresaId } :
            await Query().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (grupo is null) return NotFound();
        var supervisor = await database.Usuarios.Include(item => item.Rol).SingleOrDefaultAsync(item =>
            item.EmpresaId == current.EmpresaId && item.Id == request.SupervisorId, cancellationToken);
        if (supervisor is null) return Problem(statusCode: 400, title: "Elegí un supervisor de esta empresa.");
        if (!AccesoTrabajo.Tiene(supervisor, Permisos.GestionarTareas))
            return Problem(statusCode: 400, title: "El supervisor necesita un rol que permita gestionar tareas.");
        grupo.Nombre = request.Nombre.Trim();
        grupo.NombreNormalizado = grupo.Nombre.ToUpperInvariant();
        grupo.SupervisorId = request.SupervisorId;
        grupo.Supervisor = supervisor;
        // Crear/editar el grupo solo incluye al supervisor y conserva las membresías existentes.
        if (!grupo.Miembros.Any(item => item.UsuarioId == request.SupervisorId))
            grupo.Miembros.Add(new MiembroGrupo { EmpresaId = current.EmpresaId, Grupo = grupo, UsuarioId = request.SupervisorId });
        if (id is null)
        {
            grupo.Estados = new[] { ("Pendiente", "#6c757d"), ("En curso", "#0d6efd"), ("Terminada", "#198754") }
                .Select((item, index) => new EstadoTarea { EmpresaId = current.EmpresaId, Grupo = grupo,
                    Nombre = item.Item1, NombreNormalizado = item.Item1.ToUpperInvariant(), Color = item.Item2, Orden = index,
                    EsPredeterminado = index == 0 }).ToList();
            database.Grupos.Add(grupo);
        }
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: "23505" })
        { return Problem(statusCode: 409, title: "Ya existe un grupo con ese nombre."); }
        await transaction.CommitAsync(cancellationToken);
        return StatusCode(id is null ? 201 : 200, GrupoResponse.From(
            await Query().AsNoTracking().SingleAsync(item => item.Id == grupo.Id, cancellationToken), administracion.UsuarioActual!));
    }

    [HttpPut("{id:int}/integrantes")]
    [Authorize(Policy = Permisos.GestionarGrupos)]
    public async Task<IActionResult> SaveMembers(int id, GuardarIntegrantesRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await administracion.Begin(cancellationToken, Permisos.GestionarGrupos);
        if (transaction is null) return Forbid();
        var grupo = await Query().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (grupo is null) return NotFound();
        var ids = request.IntegrantesIds.Concat(grupo.SupervisorId is { } supervisorId ? new[] { supervisorId } : Array.Empty<int>())
            .Distinct().ToArray();
        var personas = await database.Usuarios.Where(item => item.EmpresaId == current.EmpresaId && ids.Contains(item.Id))
            .Select(item => item.Id).ToListAsync(cancellationToken);
        if (personas.Count != ids.Length) return Problem(statusCode: 400, title: "Todos los integrantes deben ser de esta empresa.");
        database.MiembrosGrupos.RemoveRange(grupo.Miembros.Where(item => !ids.Contains(item.UsuarioId)));
        var anteriores = grupo.Miembros.Select(item => item.UsuarioId).ToArray();
        foreach (var usuarioId in ids.Except(anteriores))
            grupo.Miembros.Add(new MiembroGrupo { EmpresaId = current.EmpresaId, Grupo = grupo, UsuarioId = usuarioId });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(GrupoResponse.From(await Query().AsNoTracking().SingleAsync(item => item.Id == id, cancellationToken),
            administracion.UsuarioActual!));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permisos.GestionarGrupos)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await using var transaction = await administracion.Begin(cancellationToken, Permisos.GestionarGrupos);
        if (transaction is null) return Forbid();
        var grupo = await Query().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (grupo is null) return NotFound();
        if (await database.Tareas.AnyAsync(item => item.EmpresaId == current.EmpresaId && item.GrupoId == id, cancellationToken))
            return Problem(statusCode: 409, title: "El grupo tiene tareas. Movelas o eliminálas antes de borrar el grupo.");
        database.MiembrosGrupos.RemoveRange(grupo.Miembros);
        database.EstadosTareas.RemoveRange(grupo.Estados);
        database.Grupos.Remove(grupo);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/estados")]
    public Task<IActionResult> CreateState(int id, GuardarEstadoRequest request, CancellationToken cancellationToken) =>
        SaveState(id, null, request, cancellationToken);

    [HttpPut("{id:int}/estados/{estadoId:int}")]
    public Task<IActionResult> UpdateState(int id, int estadoId, GuardarEstadoRequest request, CancellationToken cancellationToken) =>
        SaveState(id, estadoId, request, cancellationToken);

    private async Task<IActionResult> SaveState(int id, int? estadoId, GuardarEstadoRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await administracion.Begin(cancellationToken, permiso: null);
        if (transaction is null) return Forbid();
        var grupo = await Query().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (grupo is null) return NotFound();
        if (!AccesoTrabajo.Estados(administracion.UsuarioActual!, grupo)) return Forbid();
        var estado = estadoId is null ? new EstadoTarea { EmpresaId = current.EmpresaId, GrupoId = id,
            Orden = grupo.Estados.Count == 0 ? 0 : grupo.Estados.Max(item => item.Orden) + 1,
            EsPredeterminado = grupo.Estados.Count == 0 } :
            grupo.Estados.SingleOrDefault(item => item.Id == estadoId);
        if (estado is null) return NotFound();
        estado.Nombre = request.Nombre.Trim();
        estado.NombreNormalizado = estado.Nombre.ToUpperInvariant();
        estado.Color = request.Color.ToLowerInvariant();
        if (estadoId is null) database.EstadosTareas.Add(estado);
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: "23505" })
        { return Problem(statusCode: 409, title: "Ya existe un estado con ese nombre en este grupo."); }
        await transaction.CommitAsync(cancellationToken);
        return StatusCode(estadoId is null ? 201 : 200, new EstadoResponse(estado.Id, estado.Nombre, estado.Color, estado.EsPredeterminado));
    }

    [HttpPut("{id:int}/estados/{estadoId:int}/predeterminado")]
    public async Task<IActionResult> SetDefaultState(int id, int estadoId, CancellationToken cancellationToken)
    {
        await using var transaction = await administracion.Begin(cancellationToken, permiso: null);
        if (transaction is null) return Forbid();
        var grupo = await Query().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (grupo is null) return NotFound();
        if (!AccesoTrabajo.Estados(administracion.UsuarioActual!, grupo)) return Forbid();
        var estado = grupo.Estados.SingleOrDefault(item => item.Id == estadoId);
        if (estado is null) return NotFound();
        if (!estado.EsPredeterminado)
        {
            foreach (var anterior in grupo.Estados.Where(item => item.EsPredeterminado)) anterior.EsPredeterminado = false;
            // Libera el índice único antes de marcar el nuevo; ambos pasos y su auditoría son atómicos.
            await database.SaveChangesAsync(cancellationToken);
            estado.EsPredeterminado = true;
            await database.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return Ok(new EstadoResponse(estado.Id, estado.Nombre, estado.Color, estado.EsPredeterminado));
    }

    [HttpDelete("{id:int}/estados/{estadoId:int}")]
    public async Task<IActionResult> DeleteState(int id, int estadoId, [FromQuery] int? destinoId, CancellationToken cancellationToken)
    {
        await using var transaction = await administracion.Begin(cancellationToken, permiso: null);
        if (transaction is null) return Forbid();
        var grupo = await Query().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (grupo is null) return NotFound();
        if (!AccesoTrabajo.Estados(administracion.UsuarioActual!, grupo)) return Forbid();
        var estado = grupo.Estados.SingleOrDefault(item => item.Id == estadoId);
        if (estado is null) return NotFound();
        if (grupo.Estados.Count <= 1) return Problem(statusCode: 409, title: "El grupo debe conservar al menos un estado.");
        var destino = destinoId is null ? null : grupo.Estados.SingleOrDefault(item => item.Id == destinoId && item.Id != estadoId);
        if (destinoId is not null && destino is null) return Problem(statusCode: 400, title: "Elegí otro estado de este grupo.");
        var tareas = await database.Tareas.Where(item => item.EmpresaId == current.EmpresaId &&
            item.GrupoId == id && item.EstadoId == estadoId).ToListAsync(cancellationToken);
        if (estado.EsPredeterminado && destino is null)
            return Problem(statusCode: 409, title: "Elegí otro estado como predeterminado antes de eliminar este estado.");
        if (tareas.Count > 0 && destino is null) return Problem(statusCode: 409, title: "Elegí a qué estado mover las tareas antes de eliminar este estado.");
        foreach (var tarea in tareas)
        {
            tarea.Estado = destino!;
            tarea.EstadoId = destino!.Id;
            tarea.Version++;
            tarea.ActualizadaEnUtc = DateTime.UtcNow;
        }
        database.EstadosTareas.Remove(estado);
        await database.SaveChangesAsync(cancellationToken);
        if (estado.EsPredeterminado)
        {
            destino!.EsPredeterminado = true;
            await database.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return NoContent();
    }
}
