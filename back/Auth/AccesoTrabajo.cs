using back.Models;

namespace back.Auth;

public static class AccesoTrabajo
{
    public static bool Tiene(Usuario usuario, string permiso) => usuario.Rol.EsAdministrador || Permisos.DeRol(usuario.Rol).Contains(permiso);
    public static bool GestionaGrupos(Usuario usuario) => Tiene(usuario, Permisos.GestionarGrupos);
    public static bool Supervisa(Usuario usuario, GrupoTrabajo grupo) =>
        Tiene(usuario, Permisos.GestionarTareas) && (usuario.Rol.EsAdministrador || GestionaGrupos(usuario) || grupo.SupervisorId == usuario.Id);
    public static bool Estados(Usuario usuario, GrupoTrabajo grupo) => GestionaGrupos(usuario) || Supervisa(usuario, grupo);
    public static bool Ve(Usuario usuario, GrupoTrabajo grupo) => GestionaGrupos(usuario) || grupo.SupervisorId == usuario.Id ||
        grupo.Miembros.Any(item => item.UsuarioId == usuario.Id);
}
