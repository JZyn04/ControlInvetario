using back.Auth;
using back.Contracts;
using back.Data;
using back.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace back.Controllers;

[ApiController]
[Route("api/tareas")]
[Authorize(Policy = Permisos.VerTareas)]
[Authorize(Policy = "EmpresaSeleccionada")]
[AutoValidateAntiforgeryToken]
public sealed class TareasController(InventoryDbContext database, CurrentUsuario current,
    AccesoEmpresa acceso, AdministracionEmpresa administracion) : ControllerBase
{
    private IQueryable<Tarea> Query() => database.Tareas.Include(item => item.Grupo).ThenInclude(item => item.Miembros)
        .Include(item => item.Estado).Include(item => item.AsignadoA).AsSplitQuery()
        .Where(item => item.EmpresaId == current.EmpresaId);

    private static bool Visible(Usuario usuario, Tarea tarea) => usuario.Rol.EsAdministrador ||
        tarea.AsignadoAId == usuario.Id || AccesoTrabajo.Ve(usuario, tarea.Grupo);
    private IActionResult Desactualizada() => Problem(statusCode: 409, title: "La tarea cambió. Actualizá la lista antes de continuar.");
    private static void Touch(Tarea tarea) { tarea.Version++; tarea.ActualizadaEnUtc = DateTime.UtcNow; }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var usuario = (await acceso.Seleccionada())!;
        var tareas = await Query().AsNoTracking().OrderByDescending(item => item.Id).ToListAsync(ct);
        return Ok(tareas.Where(item => Visible(usuario, item)).Select(item => TareaResponse.From(item, usuario)).ToArray());
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id, CancellationToken ct)
    {
        var usuario = (await acceso.Seleccionada())!;
        var tarea = await Query().AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, ct);
        if (tarea is null || !Visible(usuario, tarea)) return NotFound();
        var dto = TareaResponse.From(tarea, usuario);
        var notas = await database.NotasTareas.AsNoTracking().Where(item => item.EmpresaId == current.EmpresaId && item.TareaId == id)
            .OrderBy(item => item.Id).ToListAsync(ct);
        var estados = await database.EstadosTareas.AsNoTracking().Where(item => item.EmpresaId == current.EmpresaId && item.GrupoId == tarea.GrupoId)
            .OrderBy(item => item.Orden).Select(item => new EstadoResponse(item.Id, item.Nombre, item.Color, item.EsPredeterminado)).ToListAsync(ct);
        return Ok(new { tarea = dto, estados, notas = notas.Select(item => NotaResponse.From(item, usuario, dto.PuedeGestionar, dto.PuedeAnotar)) });
    }

    [HttpPost]
    public Task<IActionResult> Create(GuardarTareaRequest request, CancellationToken ct) => Save(null, request, ct);
    [HttpPut("{id:int}")]
    public Task<IActionResult> Update(int id, GuardarTareaRequest request, CancellationToken ct) => Save(id, request, ct);

    private async Task<IActionResult> Save(int? id, GuardarTareaRequest request, CancellationToken ct)
    {
        await using var transaction = await administracion.Begin(ct, Permisos.GestionarTareas);
        if (transaction is null) return Forbid();
        var usuario = administracion.UsuarioActual!;
        var tarea = id is null ? new Tarea { EmpresaId = current.EmpresaId } : await Query().SingleOrDefaultAsync(item => item.Id == id, ct);
        if (tarea is null) return NotFound();
        if (id is not null)
        {
            if (!AccesoTrabajo.Supervisa(usuario, tarea.Grupo)) return Forbid();
            if (tarea.Version != request.Version) return Desactualizada();
        }
        var grupo = await database.Grupos.Include(item => item.Miembros).Include(item => item.Estados)
            .SingleOrDefaultAsync(item => item.EmpresaId == current.EmpresaId && item.Id == request.GrupoId, ct);
        if (grupo is null) return Problem(statusCode: 400, title: "Elegí un grupo de esta empresa.");
        if (!AccesoTrabajo.Supervisa(usuario, grupo)) return Forbid();
        var estado = grupo.Estados.SingleOrDefault(item => item.Id == request.EstadoId);
        if (estado is null) return Problem(statusCode: 400, title: "Elegí un estado del grupo de la tarea.");
        Usuario? asignado = null;
        if (request.AsignadoAId is not null)
        {
            asignado = await database.Usuarios.Include(item => item.Rol).SingleOrDefaultAsync(item =>
                item.EmpresaId == current.EmpresaId && item.Id == request.AsignadoAId, ct);
            if (asignado is null || !grupo.Miembros.Any(item => item.UsuarioId == asignado.Id) || !AccesoTrabajo.Tiene(asignado, Permisos.VerTareas))
                return Problem(statusCode: 400, title: "La persona debe pertenecer al grupo y tener acceso a tareas.");
        }
        if (asignado is not null && request.PermitirAutoasignacion)
            return Problem(statusCode: 400, title: "Solo una tarea sin asignar puede permitir autoasignación.");
        tarea.Grupo = grupo; tarea.GrupoId = grupo.Id; tarea.Estado = estado; tarea.EstadoId = estado.Id;
        tarea.Titulo = request.Titulo.Trim(); tarea.Descripcion = request.Descripcion.Trim();
        tarea.AsignadoA = asignado; tarea.AsignadoAId = asignado?.Id;
        tarea.PermitirAutoasignacion = request.PermitirAutoasignacion;
        if (id is null) database.Tareas.Add(tarea); else Touch(tarea);
        await database.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return StatusCode(id is null ? 201 : 200, TareaResponse.From(tarea, usuario));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, [FromQuery] int version, CancellationToken ct)
    {
        await using var transaction = await administracion.Begin(ct, Permisos.GestionarTareas);
        if (transaction is null) return Forbid();
        var tarea = await Query().SingleOrDefaultAsync(item => item.Id == id, ct);
        if (tarea is null) return NotFound();
        if (!AccesoTrabajo.Supervisa(administracion.UsuarioActual!, tarea.Grupo)) return Forbid();
        if (version != tarea.Version) return Desactualizada();
        database.NotasTareas.RemoveRange(await database.NotasTareas.Where(item => item.EmpresaId == current.EmpresaId && item.TareaId == id).ToListAsync(ct));
        database.Tareas.Remove(tarea);
        await database.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:int}/tomar")]
    public async Task<IActionResult> Claim(int id, TomarTareaRequest request, CancellationToken ct)
    {
        await using var transaction = await administracion.Begin(ct, Permisos.AutoasignarTareas);
        if (transaction is null) return Forbid();
        var usuario = administracion.UsuarioActual!;
        var tarea = await Query().SingleOrDefaultAsync(item => item.Id == id, ct);
        if (tarea is null || !Visible(usuario, tarea)) return NotFound();
        if (!tarea.Grupo.Miembros.Any(item => item.UsuarioId == usuario.Id)) return Forbid();
        if (tarea.Version != request.Version) return Desactualizada();
        if (tarea.AsignadoAId is not null || !tarea.PermitirAutoasignacion)
            return Problem(statusCode: 409, title: "Esta tarea no está disponible para autoasignación.");
        tarea.AsignadoAId = usuario.Id; tarea.PermitirAutoasignacion = false; Touch(tarea);
        await database.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Ok(TareaResponse.From(await Query().AsNoTracking().SingleAsync(item => item.Id == id, ct), usuario));
    }

    [HttpPut("{id:int}/estado")]
    public async Task<IActionResult> ChangeState(int id, CambiarEstadoRequest request, CancellationToken ct)
    {
        await using var transaction = await administracion.Begin(ct, permiso: null);
        if (transaction is null) return Forbid();
        var usuario = administracion.UsuarioActual!;
        if (!AccesoTrabajo.Tiene(usuario, Permisos.VerTareas)) return Forbid();
        var tarea = await Query().SingleOrDefaultAsync(item => item.Id == id, ct);
        if (tarea is null || !Visible(usuario, tarea)) return NotFound();
        if (!TareaResponse.From(tarea, usuario).PuedeCambiarEstado) return Forbid();
        if (request.Version != tarea.Version) return Desactualizada();
        var estado = await database.EstadosTareas.SingleOrDefaultAsync(item => item.EmpresaId == current.EmpresaId &&
            item.GrupoId == tarea.GrupoId && item.Id == request.EstadoId, ct);
        if (estado is null) return Problem(statusCode: 400, title: "Elegí un estado de este grupo.");
        tarea.Estado = estado; tarea.EstadoId = estado.Id; Touch(tarea);
        await database.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Ok(TareaResponse.From(tarea, usuario));
    }

    [HttpPost("{id:int}/notas")]
    public Task<IActionResult> CreateNote(int id, GuardarNotaRequest request, CancellationToken ct) => SaveNote(id, null, request, ct);
    [HttpPut("{id:int}/notas/{notaId:int}")]
    public Task<IActionResult> UpdateNote(int id, int notaId, GuardarNotaRequest request, CancellationToken ct) => SaveNote(id, notaId, request, ct);

    private async Task<IActionResult> SaveNote(int id, int? notaId, GuardarNotaRequest request, CancellationToken ct)
    {
        await using var transaction = await administracion.Begin(ct, permiso: null);
        if (transaction is null) return Forbid();
        var usuario = administracion.UsuarioActual!;
        if (!AccesoTrabajo.Tiene(usuario, Permisos.VerTareas)) return Forbid();
        var tarea = await Query().SingleOrDefaultAsync(item => item.Id == id, ct);
        if (tarea is null || !Visible(usuario, tarea)) return NotFound();
        var dto = TareaResponse.From(tarea, usuario);
        if (!dto.PuedeAnotar) return Forbid();
        var nota = notaId is null ? new NotaTarea { EmpresaId = current.EmpresaId, TareaId = id,
            AutorId = usuario.Id, CorreoAutor = usuario.Correo, TipoAutor = tarea.AsignadoAId == usuario.Id ? "Asignado" : "Supervisor" } :
            await database.NotasTareas.SingleOrDefaultAsync(item => item.EmpresaId == current.EmpresaId && item.TareaId == id && item.Id == notaId, ct);
        if (nota is null) return NotFound();
        if (!NotaResponse.From(nota, usuario, dto.PuedeGestionar, dto.PuedeAnotar).PuedeEditar) return Forbid();
        nota.Titulo = request.Titulo.Trim(); nota.Descripcion = request.Descripcion.Trim(); nota.ActualizadaEnUtc = DateTime.UtcNow;
        if (notaId is null) database.NotasTareas.Add(nota);
        await database.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return StatusCode(notaId is null ? 201 : 200, NotaResponse.From(nota, usuario, dto.PuedeGestionar, dto.PuedeAnotar));
    }

    [HttpDelete("{id:int}/notas/{notaId:int}")]
    public async Task<IActionResult> DeleteNote(int id, int notaId, CancellationToken ct)
    {
        await using var transaction = await administracion.Begin(ct, permiso: null);
        if (transaction is null) return Forbid();
        var usuario = administracion.UsuarioActual!;
        if (!AccesoTrabajo.Tiene(usuario, Permisos.VerTareas)) return Forbid();
        var tarea = await Query().SingleOrDefaultAsync(item => item.Id == id, ct);
        if (tarea is null || !Visible(usuario, tarea)) return NotFound();
        var dto = TareaResponse.From(tarea, usuario);
        var nota = await database.NotasTareas.SingleOrDefaultAsync(item => item.EmpresaId == current.EmpresaId && item.TareaId == id && item.Id == notaId, ct);
        if (nota is null) return NotFound();
        if (!NotaResponse.From(nota, usuario, dto.PuedeGestionar, dto.PuedeAnotar).PuedeEditar) return Forbid();
        database.NotasTareas.Remove(nota);
        await database.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return NoContent();
    }
}
