using back.Models;

namespace back.Auth;

public static class Permisos
{
    public const string AdministrarEmpresa = "empresa.administrar";
    public const string VerInventario = "inventario.ver";
    public const string CrearInventario = "inventario.crear";
    public const string EditarInventario = "inventario.editar";
    public const string EliminarInventario = "inventario.eliminar";
    public const string EntradasInventario = "inventario.entradas";
    public const string SalidasInventario = "inventario.salidas";
    public const string PrestamosInventario = "inventario.prestamos";
    public const string ConteosInventario = "inventario.conteos";
    public const string CategoriasInventario = "inventario.categorias";
    public const string BodegasInventario = "inventario.bodegas";
    public const string TrasladosInventario = "inventario.traslados";
    public const string ExportarInventario = "inventario.exportar";
    public const string VerTareas = "tareas.ver";
    public const string GestionarTareas = "tareas.gestionar";
    public const string AutoasignarTareas = "tareas.autoasignar";
    public const string AnotarTareas = "tareas.anotar";
    public const string CambiarEstadoTareas = "tareas.estado";
    public const string VerGrupos = "grupos.ver";
    public const string GestionarGrupos = "grupos.gestionar";
    private static readonly (string Clave, string Nombre, PermisosInventario Valor)[] Inventario =
    [
        (VerInventario, "Ver inventario", PermisosInventario.Ver),
        (CrearInventario, "Crear productos", PermisosInventario.Crear),
        (EditarInventario, "Editar productos", PermisosInventario.Editar),
        (EliminarInventario, "Eliminar productos", PermisosInventario.Eliminar),
        (EntradasInventario, "Registrar entradas y devoluciones", PermisosInventario.Entradas),
        (SalidasInventario, "Registrar ventas y consumos", PermisosInventario.Salidas),
        (PrestamosInventario, "Registrar préstamos y sus devoluciones", PermisosInventario.Prestamos),
        (ConteosInventario, "Registrar conteos físicos y ajustes", PermisosInventario.Conteos),
        (CategoriasInventario, "Gestionar categorías", PermisosInventario.Categorias),
        (BodegasInventario, "Gestionar bodegas", PermisosInventario.Bodegas),
        (TrasladosInventario, "Trasladar existencias entre bodegas", PermisosInventario.Traslados),
        (ExportarInventario, "Exportar inventario y kárdex", PermisosInventario.Exportar)
    ];

    private static readonly (string Clave, string Nombre, PermisosTareas Valor)[] Tareas =
    [
        (VerTareas, "Ver tareas", PermisosTareas.Ver),
        (GestionarTareas, "Gestionar y supervisar tareas", PermisosTareas.Gestionar),
        (AutoasignarTareas, "Tomar tareas disponibles", PermisosTareas.Autoasignar),
        (AnotarTareas, "Escribir notas de tareas", PermisosTareas.Anotar),
        (CambiarEstadoTareas, "Cambiar estado de tareas asignadas", PermisosTareas.CambiarEstado)
    ];

    private static readonly (string Clave, string Nombre, PermisosGrupos Valor)[] Grupos =
    [
        (VerGrupos, "Ver grupos propios", PermisosGrupos.Ver),
        (GestionarGrupos, "Gestionar grupos, integrantes y estados", PermisosGrupos.Gestionar)
    ];

    public static readonly (string Clave, string Nombre)[] Catalogo = Inventario
        .Select(item => (item.Clave, item.Nombre)).Concat(Tareas.Select(item => (item.Clave, item.Nombre)))
        .Concat(Grupos.Select(item => (item.Clave, item.Nombre))).ToArray();

    public static string[] DeRol(Rol rol) => Inventario
        .Where(item => rol.EsAdministrador || rol.Permisos.HasFlag(item.Valor)).Select(item => item.Clave)
        .Concat(Tareas.Where(item => rol.EsAdministrador || rol.PermisosTareas.HasFlag(item.Valor)).Select(item => item.Clave))
        .Concat(Grupos.Where(item => rol.EsAdministrador || rol.PermisosGrupos.HasFlag(item.Valor)).Select(item => item.Clave)).ToArray();

    public static bool TryParse(IReadOnlyList<string>? claves, out PermisosInventario permisos, out PermisosTareas tareas,
        out PermisosGrupos grupos)
    {
        permisos = PermisosInventario.Ninguno;
        tareas = PermisosTareas.Ninguno;
        grupos = PermisosGrupos.Ninguno;
        if (claves is null) return false;
        foreach (var clave in claves)
        {
            var item = Inventario.FirstOrDefault(item => item.Clave == clave);
            var tarea = Tareas.FirstOrDefault(item => item.Clave == clave);
            var grupo = Grupos.FirstOrDefault(item => item.Clave == clave);
            if (item.Clave is null && tarea.Clave is null && grupo.Clave is null) return false;
            permisos |= item.Valor;
            tareas |= tarea.Valor;
            grupos |= grupo.Valor;
        }
        // Para modificar inventario primero hay que poder consultarlo.
        return (permisos == PermisosInventario.Ninguno || permisos.HasFlag(PermisosInventario.Ver)) &&
            (tareas == PermisosTareas.Ninguno || tareas.HasFlag(PermisosTareas.Ver)) &&
            (grupos == PermisosGrupos.Ninguno || grupos.HasFlag(PermisosGrupos.Ver));
    }
}
