using System.ComponentModel.DataAnnotations;
using back.Auth;
using back.Models;

namespace back.Contracts;

public sealed record GuardarRolRequest([Required, MaxLength(80)] string Nombre, [Required] IReadOnlyList<string> Permisos);
public sealed record AsignarRolRequest([Range(1, int.MaxValue)] int RolId);
public sealed record PermisoResponse(string Clave, string Nombre);
public sealed record RolResponse(int Id, string Nombre, bool EsSistema, bool EsAdministrador, IReadOnlyList<string> Permisos)
{
    public static RolResponse From(Rol rol) => new(rol.Id, rol.Nombre, rol.EsSistema, rol.EsAdministrador, Auth.Permisos.DeRol(rol));
}
