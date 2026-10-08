using System.ComponentModel.DataAnnotations;
using back.Auth;
using back.Models;

namespace back.Contracts;

public sealed record GuardarGrupoRequest([Required, MaxLength(100)] string Nombre,
    [Range(1, int.MaxValue)] int SupervisorId);
public sealed record GuardarIntegrantesRequest([Required] IReadOnlyList<int> IntegrantesIds);
public sealed record GuardarEstadoRequest([Required, MaxLength(80)] string Nombre,
    [Required, RegularExpression(@"^#[0-9a-fA-F]{6}$")] string Color);
public sealed record GuardarTareaRequest([Range(1, int.MaxValue)] int GrupoId,
    [Range(1, int.MaxValue)] int EstadoId, [Required, MaxLength(140)] string Titulo,
    [Required, MaxLength(4000)] string Descripcion, int? AsignadoAId, bool PermitirAutoasignacion, int Version = 0);
public sealed record CambiarEstadoRequest([Range(1, int.MaxValue)] int EstadoId, [Range(1, int.MaxValue)] int Version);
public sealed record TomarTareaRequest([Range(1, int.MaxValue)] int Version);
public sealed record GuardarNotaRequest([Required, MaxLength(140)] string Titulo, [Required, MaxLength(4000)] string Descripcion);
public sealed record PersonaTrabajoResponse(int Id, string Correo, bool PuedeVerTareas, bool PuedeSupervisar)
{
    public static PersonaTrabajoResponse From(Usuario usuario) => new(usuario.Id, usuario.Correo,
        AccesoTrabajo.Tiene(usuario, Permisos.VerTareas), AccesoTrabajo.Tiene(usuario, Permisos.GestionarTareas));
}
public sealed record EstadoResponse(int Id, string Nombre, string Color, bool EsPredeterminado = false);
public sealed record GrupoResponse(int Id, string Nombre, PersonaTrabajoResponse? Supervisor,
    IReadOnlyList<PersonaTrabajoResponse> Integrantes, IReadOnlyList<EstadoResponse> Estados,
    bool PuedeGestionarTareas, bool PuedeGestionarEstados)
{
    public static GrupoResponse From(GrupoTrabajo grupo, Usuario usuario) => new(grupo.Id, grupo.Nombre,
        grupo.Supervisor is null ? null : PersonaTrabajoResponse.From(grupo.Supervisor),
        grupo.Miembros.OrderBy(item => item.Usuario.Correo).Select(item => PersonaTrabajoResponse.From(item.Usuario)).ToArray(),
        grupo.Estados.OrderBy(item => item.Orden).ThenBy(item => item.Id).Select(item => new EstadoResponse(item.Id, item.Nombre, item.Color, item.EsPredeterminado)).ToArray(),
        AccesoTrabajo.Supervisa(usuario, grupo), AccesoTrabajo.Estados(usuario, grupo));
}
public sealed record TareaResponse(int Id, int GrupoId, string Grupo, string Titulo, string Descripcion,
    int? AsignadoAId, string? AsignadoACorreo, bool PermitirAutoasignacion, EstadoResponse Estado,
    int Version, DateTime CreadaEnUtc, DateTime ActualizadaEnUtc, bool PuedeGestionar,
    bool PuedeTomar, bool PuedeAnotar, bool PuedeCambiarEstado)
{
    public static TareaResponse From(Tarea tarea, Usuario usuario)
    {
        var supervisor = AccesoTrabajo.Supervisa(usuario, tarea.Grupo);
        var asignado = tarea.AsignadoAId == usuario.Id;
        return new(tarea.Id, tarea.GrupoId, tarea.Grupo.Nombre, tarea.Titulo, tarea.Descripcion,
            tarea.AsignadoAId, tarea.AsignadoA?.Correo, tarea.PermitirAutoasignacion,
            new(tarea.Estado.Id, tarea.Estado.Nombre, tarea.Estado.Color, tarea.Estado.EsPredeterminado), tarea.Version, tarea.CreadaEnUtc, tarea.ActualizadaEnUtc,
            supervisor, tarea.AsignadoAId is null && tarea.PermitirAutoasignacion &&
                AccesoTrabajo.Tiene(usuario, Permisos.AutoasignarTareas) && tarea.Grupo.Miembros.Any(item => item.UsuarioId == usuario.Id),
            supervisor || (asignado && AccesoTrabajo.Tiene(usuario, Permisos.AnotarTareas)),
            supervisor || (asignado && AccesoTrabajo.Tiene(usuario, Permisos.CambiarEstadoTareas)));
    }
}
public sealed record NotaResponse(int Id, string Titulo, string Descripcion, int? AutorId, string CorreoAutor,
    string TipoAutor, DateTime CreadaEnUtc, DateTime ActualizadaEnUtc, bool PuedeEditar)
{
    public static NotaResponse From(NotaTarea nota, Usuario usuario, bool supervisor, bool puedeAnotar) => new(nota.Id,
        nota.Titulo, nota.Descripcion, nota.AutorId, nota.CorreoAutor, nota.TipoAutor, nota.CreadaEnUtc, nota.ActualizadaEnUtc,
        supervisor || (puedeAnotar && nota.AutorId == usuario.Id));
}
