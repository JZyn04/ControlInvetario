using back.Models;

namespace back.Auth;

public static class Permisos
{
    public const string AdministrarEmpresa = "empresa.administrar";
    public const string VerInventario = "inventario.ver";
    public const string CrearInventario = "inventario.crear";
    public const string EditarInventario = "inventario.editar";
    public const string EliminarInventario = "inventario.eliminar";
    public static readonly (string Clave, string Nombre, PermisosInventario Valor)[] Catalogo =
    [
        (VerInventario, "Ver inventario", PermisosInventario.Ver),
        (CrearInventario, "Crear productos", PermisosInventario.Crear),
        (EditarInventario, "Editar productos", PermisosInventario.Editar),
        (EliminarInventario, "Eliminar productos", PermisosInventario.Eliminar)
    ];

    public static string[] DeRol(Rol rol) => Catalogo
        .Where(item => rol.EsAdministrador || rol.Permisos.HasFlag(item.Valor)).Select(item => item.Clave).ToArray();

    public static bool TryParse(IReadOnlyList<string>? claves, out PermisosInventario permisos)
    {
        permisos = PermisosInventario.Ninguno;
        if (claves is null) return false;
        foreach (var clave in claves)
        {
            var item = Catalogo.FirstOrDefault(item => item.Clave == clave);
            if (item.Clave is null) return false;
            permisos |= item.Valor;
        }
        // Para modificar inventario primero hay que poder consultarlo.
        return permisos == PermisosInventario.Ninguno || permisos.HasFlag(PermisosInventario.Ver);
    }
}
