using System.Security.Claims;
using back.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace back.Data;

public sealed record AuditoriaCambios(object Entidad, string Accion, string[] Campos)
{
    public static bool EsCambio(EntityEntry entry) =>
        entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted &&
        entry.Entity is Empresa or Usuario or Rol or Product or GrupoTrabajo or MiembroGrupo or EstadoTarea or Tarea or NotaTarea or PrestamoInventario or CategoriaInventario or BodegaInventario;

    public static AuditoriaCambios Capturar(EntityEntry entry) => new(entry.Entity,
        entry.State == EntityState.Added ? "Creado" : entry.State == EntityState.Deleted ? "Eliminado" : "Editado",
        entry.Properties.Where(item => item.IsModified && item.Metadata.Name != "CorreoNormalizado")
            .Select(item => item.Metadata.Name == "ContraseniaHash" ? "Contraseña" : item.Metadata.Name).ToArray());

    public RegistroAuditoria Crear(HttpContext? context, IReadOnlyList<AuditoriaCambios> cambios)
    {
        var (empresaId, entidad, id, nombre) = Entidad switch
        {
            Empresa e => (e.Id, "Empresa", e.Id.ToString(), e.Nombre),
            Usuario u => (u.EmpresaId, "Usuario", u.Id.ToString(), u.Correo),
            Rol r => (r.EmpresaId, "Rol", r.Id.ToString(), r.Nombre),
            Product p => (p.EmpresaId, "Producto", p.Id.ToString(), p.Name),
            GrupoTrabajo g => (g.EmpresaId, "Grupo", g.Id.ToString(), g.Nombre),
            MiembroGrupo m => (m.EmpresaId, "Integrante", $"{m.GrupoId}/{m.UsuarioId}", $"Grupo {m.GrupoId}, usuario {m.UsuarioId}"),
            EstadoTarea e => (e.EmpresaId, "Estado", e.Id.ToString(), e.Nombre),
            Tarea t => (t.EmpresaId, "Tarea", t.Id.ToString(), t.Titulo),
            NotaTarea n => (n.EmpresaId, "Nota", n.Id.ToString(), n.Titulo),
            PrestamoInventario p => (p.EmpresaId, "Prestamo", p.Id.ToString(), p.Destinatario),
            CategoriaInventario c => (c.EmpresaId, "Categoria", c.Id.ToString(), c.Nombre),
            BodegaInventario b => (b.EmpresaId, "Bodega", b.Id.ToString(), b.Nombre),
            _ => throw new InvalidOperationException("Entidad no auditable.")
        };
        int? autorId = null;
        var correo = "Sistema";
        if (int.TryParse(context?.User.FindFirstValue("EmpresaId"), out var empresaActual) && empresaActual == empresaId &&
            int.TryParse(context?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
        {
            autorId = usuarioId;
            correo = context!.User.FindFirstValue(ClaimTypes.Name) ?? "Usuario";
        }
        else if (cambios.Select(item => item.Entidad).OfType<Usuario>().FirstOrDefault(item =>
            item.EmpresaId == empresaId && item.EsPrincipal) is { } principal)
        {
            autorId = principal.Id;
            correo = principal.Correo;
        }
        var detalle = nombre + (Campos.Length == 0 ? "" : " · Campos: " + string.Join(", ", Campos));
        return new RegistroAuditoria
        {
            EmpresaId = empresaId, AutorId = autorId, CorreoAutor = correo, Accion = Accion,
            Entidad = entidad, EntidadId = id, Detalle = detalle[..Math.Min(detalle.Length, 600)]
        };
    }
}
