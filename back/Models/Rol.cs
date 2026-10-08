using System.ComponentModel.DataAnnotations.Schema;

namespace back.Models;

[Flags]
public enum PermisosInventario
{
    Ninguno = 0, Ver = 1, Crear = 2, Editar = 4, Eliminar = 8,
    Entradas = 16, Salidas = 32, Prestamos = 64, Conteos = 128,
    Categorias = 256, Bodegas = 512, Traslados = 1024, Exportar = 2048, Todos = 4095
}

[Flags]
public enum PermisosTareas { Ninguno = 0, Ver = 1, Gestionar = 2, Autoasignar = 4, Anotar = 8, CambiarEstado = 16, Todos = 31 }
[Flags]
public enum PermisosGrupos { Ninguno = 0, Ver = 1, Gestionar = 2, Todos = 3 }

public sealed class Rol
{
    public const string Administrador = "AdministradorEmpresa";
    public const string Operador = "OperadorInventario";
    public const string SupervisorTareas = "SupervisorTareas";
    public const string ColaboradorTareas = "ColaboradorTareas";
    public const string GestorGrupos = "GestorGrupos";
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;
    public string Nombre { get; set; } = string.Empty;
    public string NombreNormalizado { get; set; } = string.Empty;
    public string? CodigoSistema { get; set; }
    public PermisosInventario Permisos { get; set; }
    public PermisosTareas PermisosTareas { get; set; }
    public PermisosGrupos PermisosGrupos { get; set; }
    [NotMapped] public bool EsSistema => CodigoSistema is not null;
    [NotMapped] public bool EsAdministrador => CodigoSistema == Administrador;

    public static Rol[] CrearSistema(Empresa empresa) =>
    [
        new() { Empresa = empresa, Nombre = "Administrador de empresa", NombreNormalizado = "ADMINISTRADOR DE EMPRESA", CodigoSistema = Administrador, Permisos = PermisosInventario.Todos, PermisosTareas = PermisosTareas.Todos, PermisosGrupos = PermisosGrupos.Todos },
        new() { Empresa = empresa, Nombre = "Operador de inventario", NombreNormalizado = "OPERADOR DE INVENTARIO", CodigoSistema = Operador, Permisos = PermisosInventario.Todos },
        new() { Empresa = empresa, Nombre = "Supervisor de tareas", NombreNormalizado = "SUPERVISOR DE TAREAS", CodigoSistema = SupervisorTareas, PermisosTareas = PermisosTareas.Todos, PermisosGrupos = PermisosGrupos.Ver },
        new() { Empresa = empresa, Nombre = "Colaborador de tareas", NombreNormalizado = "COLABORADOR DE TAREAS", CodigoSistema = ColaboradorTareas, PermisosTareas = PermisosTareas.Ver | PermisosTareas.Autoasignar | PermisosTareas.Anotar | PermisosTareas.CambiarEstado, PermisosGrupos = PermisosGrupos.Ver },
        new() { Empresa = empresa, Nombre = "Gestor de grupos", NombreNormalizado = "GESTOR DE GRUPOS", CodigoSistema = GestorGrupos, PermisosGrupos = PermisosGrupos.Todos }
    ];
}
