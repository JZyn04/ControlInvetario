using System.ComponentModel.DataAnnotations.Schema;

namespace back.Models;

[Flags]
public enum PermisosInventario { Ninguno = 0, Ver = 1, Crear = 2, Editar = 4, Eliminar = 8, Todos = 15 }

public sealed class Rol
{
    public const string Administrador = "AdministradorEmpresa";
    public const string Operador = "OperadorInventario";
    public int Id { get; set; }
    public int EmpresaId { get; set; }
    public Empresa Empresa { get; set; } = null!;
    public string Nombre { get; set; } = string.Empty;
    public string NombreNormalizado { get; set; } = string.Empty;
    public string? CodigoSistema { get; set; }
    public PermisosInventario Permisos { get; set; }
    [NotMapped] public bool EsSistema => CodigoSistema is not null;
    [NotMapped] public bool EsAdministrador => CodigoSistema == Administrador;

    public static Rol[] CrearSistema(Empresa empresa) =>
    [
        new() { Empresa = empresa, Nombre = "Administrador de empresa", NombreNormalizado = "ADMINISTRADOR DE EMPRESA", CodigoSistema = Administrador, Permisos = PermisosInventario.Todos },
        new() { Empresa = empresa, Nombre = "Operador de inventario", NombreNormalizado = "OPERADOR DE INVENTARIO", CodigoSistema = Operador, Permisos = PermisosInventario.Todos }
    ];
}
