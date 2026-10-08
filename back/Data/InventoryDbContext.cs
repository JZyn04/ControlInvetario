using back.Models;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace back.Data;

public sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options, IHttpContextAccessor? accessor = null)
    : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<Tarea> Tareas => Set<Tarea>();
    public DbSet<NotaTarea> NotasTareas => Set<NotaTarea>();
    public DbSet<GrupoTrabajo> Grupos => Set<GrupoTrabajo>();
    public DbSet<MiembroGrupo> MiembrosGrupos => Set<MiembroGrupo>();
    public DbSet<EstadoTarea> EstadosTareas => Set<EstadoTarea>();
    public DbSet<RegistroAuditoria> Auditoria => Set<RegistroAuditoria>();
    public DbSet<MovimientoInventario> MovimientosInventario => Set<MovimientoInventario>();
    public DbSet<PrestamoInventario> PrestamosInventario => Set<PrestamoInventario>();
    public DbSet<CategoriaInventario> CategoriasInventario => Set<CategoriaInventario>();
    public DbSet<BodegaInventario> BodegasInventario => Set<BodegaInventario>();
    public DbSet<ExistenciaBodega> ExistenciasBodegas => Set<ExistenciaBodega>();
    public DbSet<ProductoInventarioHistorico> ProductosHistoricos => Set<ProductoInventarioHistorico>();
    public DbSet<BodegaInventarioHistorica> BodegasHistoricas => Set<BodegaInventarioHistorica>();
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var product = modelBuilder.Entity<Product>();
        product.ToTable("products", table =>
        {
            table.HasCheckConstraint("CK_products_Cantidades", "\"Quantity\" >= 0 AND \"MinimumStock\" >= 0 AND (\"AllowsFractions\" OR (trunc(\"Quantity\") = \"Quantity\" AND trunc(\"MinimumStock\") = \"MinimumStock\"))");
        });
        product.HasKey(item => item.Id);
        product.HasIndex(item => new { item.EmpresaId, item.BodegaId, item.Code }).IsUnique();
        product.HasOne<Empresa>().WithMany().HasForeignKey(item => item.EmpresaId)
            .OnDelete(DeleteBehavior.Cascade);
        product.Property(item => item.Code).HasMaxLength(40);
        product.Property(item => item.Name).HasMaxLength(120);
        product.Property(item => item.UnitPrice).HasPrecision(12, 2);
        product.Property(item => item.Quantity).HasPrecision(15, 3);
        product.Property(item => item.MinimumStock).HasPrecision(15, 3);
        product.Property(item => item.Unit).HasMaxLength(30).HasDefaultValue("Unidad").IsRequired();
        product.Property(item => item.IsActive).HasDefaultValue(true);
        product.Property(item => item.Version).HasDefaultValue(1L).ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
        product.HasOne<CategoriaInventario>().WithMany().HasForeignKey(p => new { p.EmpresaId, p.CategoriaId })
            .HasPrincipalKey(c => new { c.EmpresaId, c.Id }).IsRequired().OnDelete(DeleteBehavior.Restrict);
        product.HasOne<BodegaInventario>().WithMany().HasForeignKey(p => new { p.EmpresaId, p.BodegaId })
            .HasPrincipalKey(b => new { b.EmpresaId, b.Id }).OnDelete(DeleteBehavior.Restrict);

        var productoHistorico = modelBuilder.Entity<ProductoInventarioHistorico>();
        productoHistorico.ToTable("ProductoInventarioHistorico");
        productoHistorico.HasKey(p => new { p.EmpresaId, p.Id });
        productoHistorico.Property(p => p.Id).ValueGeneratedNever();
        productoHistorico.Property(p => p.Code).HasMaxLength(40).IsRequired();
        productoHistorico.Property(p => p.Name).HasMaxLength(120).IsRequired();
        productoHistorico.Property(p => p.Unit).HasMaxLength(30).IsRequired();
        productoHistorico.Property(p => p.MinimumStock).HasPrecision(15, 3);
        productoHistorico.Property(p => p.UnitPrice).HasPrecision(12, 2);
        productoHistorico.HasOne<BodegaInventarioHistorica>().WithMany().HasForeignKey(p => new { p.EmpresaId, p.BodegaId })
            .HasPrincipalKey(b => new { b.EmpresaId, b.Id }).OnDelete(DeleteBehavior.Restrict);
        var bodegaHistorica = modelBuilder.Entity<BodegaInventarioHistorica>();
        bodegaHistorica.ToTable("BodegaInventarioHistorica");
        bodegaHistorica.HasKey(b => new { b.EmpresaId, b.Id });
        bodegaHistorica.Property(b => b.Id).ValueGeneratedNever();
        bodegaHistorica.Property(b => b.Nombre).HasMaxLength(100).IsRequired();
        bodegaHistorica.HasOne<Empresa>().WithMany().HasForeignKey(b => b.EmpresaId).OnDelete(DeleteBehavior.Cascade);

        var categoria = modelBuilder.Entity<CategoriaInventario>();
        categoria.ToTable("CategoriaInventario");
        categoria.Property(c => c.Nombre).HasMaxLength(100).IsRequired();
        categoria.Property(c => c.NombreNormalizado).HasMaxLength(100).IsRequired();
        categoria.HasIndex(c => new { c.EmpresaId, c.NombreNormalizado }).IsUnique();
        categoria.HasOne<Empresa>().WithMany().HasForeignKey(c => c.EmpresaId).OnDelete(DeleteBehavior.Cascade);
        var bodega = modelBuilder.Entity<BodegaInventario>();
        bodega.ToTable("BodegaInventario");
        bodega.Property(b => b.Nombre).HasMaxLength(100).IsRequired();
        bodega.Property(b => b.NombreNormalizado).HasMaxLength(100).IsRequired();
        bodega.HasIndex(b => new { b.EmpresaId, b.NombreNormalizado }).IsUnique();
        bodega.HasIndex(b => b.EmpresaId).IsUnique().HasFilter("\"Predeterminada\"");
        bodega.HasOne<Empresa>().WithMany().HasForeignKey(b => b.EmpresaId).OnDelete(DeleteBehavior.Cascade);
        var existencia = modelBuilder.Entity<ExistenciaBodega>();
        existencia.ToTable("ExistenciaBodega", t => t.HasCheckConstraint("CK_ExistenciaBodega_Cantidad", "\"Cantidad\" >= 0"));
        existencia.HasKey(e => new { e.EmpresaId, e.ProductoId, e.BodegaId });
        existencia.Property(e => e.Cantidad).HasPrecision(15, 3);
        existencia.HasOne<Product>().WithMany().HasForeignKey(e => new { e.EmpresaId, e.ProductoId, e.BodegaId })
            .HasPrincipalKey(p => new { p.EmpresaId, p.Id, p.BodegaId }).OnDelete(DeleteBehavior.Cascade);
        existencia.HasOne<BodegaInventario>().WithMany().HasForeignKey(e => new { e.EmpresaId, e.BodegaId })
            .HasPrincipalKey(b => new { b.EmpresaId, b.Id }).OnDelete(DeleteBehavior.Restrict);

        var prestamo = modelBuilder.Entity<PrestamoInventario>();
        prestamo.ToTable("PrestamoInventario", table =>
        {
            table.HasCheckConstraint("CK_PrestamoInventario_Cantidades", "\"Cantidad\" > 0 AND \"Devuelta\" >= 0 AND \"Devuelta\" <= \"Cantidad\"");
            table.HasCheckConstraint("CK_PrestamoInventario_Destinatario", "length(trim(\"Destinatario\")) > 0 AND (NOT \"EsExterno\" OR \"DestinatarioUsuarioId\" IS NULL)");
        });
        prestamo.Property(p => p.Cantidad).HasPrecision(15, 3);
        prestamo.Property(p => p.Devuelta).HasPrecision(15, 3);
        prestamo.Property(p => p.Destinatario).HasMaxLength(254).IsRequired();
        prestamo.HasOne(p => p.Producto).WithMany().HasForeignKey(p => new { p.EmpresaId, p.ProductoId })
            .HasPrincipalKey(p => new { p.EmpresaId, p.Id }).OnDelete(DeleteBehavior.Restrict);
        prestamo.HasOne<Usuario>().WithMany().HasForeignKey(p => new { p.EmpresaId, p.DestinatarioUsuarioId })
            .HasPrincipalKey(p => new { p.EmpresaId, p.Id }).OnDelete(DeleteBehavior.Restrict);
        prestamo.HasIndex(p => new { p.EmpresaId, p.ProductoId, p.Id });
        prestamo.HasOne<BodegaInventarioHistorica>().WithMany().HasForeignKey(p => new { p.EmpresaId, p.BodegaOrigenId })
            .HasPrincipalKey(b => new { b.EmpresaId, b.Id }).OnDelete(DeleteBehavior.Restrict);

        var movimiento = modelBuilder.Entity<MovimientoInventario>();
        movimiento.ToTable("MovimientoInventario", table =>
        {
            table.HasCheckConstraint("CK_MovimientoInventario_Saldos", "\"SaldoAnterior\" >= 0 AND \"SaldoPosterior\" >= 0 AND \"SaldoAnterior\" + \"Cambio\" = \"SaldoPosterior\" AND \"Cantidad\" >= 0");
            table.HasCheckConstraint("CK_MovimientoInventario_Tipo", "\"Tipo\" IN ('Inicial', 'Entrada', 'Venta', 'Consumo', 'Prestamo', 'DevolucionPrestamo', 'Devolucion', 'Conteo', 'Ajuste', 'TrasladoSalida', 'TrasladoEntrada')");
            table.HasCheckConstraint("CK_MovimientoInventario_Bodega", "\"SaldoBodegaAnterior\" >= 0 AND \"SaldoBodegaPosterior\" >= 0 AND \"SaldoBodegaAnterior\" + \"CambioBodega\" = \"SaldoBodegaPosterior\"");
        });
        movimiento.Property(m => m.Tipo).HasMaxLength(30).IsRequired();
        movimiento.Property(m => m.Unidad).HasMaxLength(30).IsRequired();
        foreach (var nombre in new[] { "Cantidad", "SaldoAnterior", "Cambio", "SaldoPosterior" })
            movimiento.Property<decimal>(nombre).HasPrecision(15, 3);
        foreach (var nombre in new[] { "SaldoBodegaAnterior", "CambioBodega", "SaldoBodegaPosterior" })
            movimiento.Property<decimal>(nombre).HasPrecision(15, 3);
        movimiento.HasOne<BodegaInventarioHistorica>().WithMany().HasForeignKey(m => new { m.EmpresaId, m.BodegaId })
            .HasPrincipalKey(b => new { b.EmpresaId, b.Id }).OnDelete(DeleteBehavior.Restrict);
        movimiento.Property(m => m.PrecioUnitario).HasPrecision(12, 2);
        movimiento.Property(m => m.Total).HasPrecision(24, 2);
        movimiento.Property(m => m.Motivo).HasMaxLength(500).IsRequired();
        movimiento.Property(m => m.Referencia).HasMaxLength(100);
        movimiento.Property(m => m.Cliente).HasMaxLength(254);
        movimiento.Property(m => m.CorreoAutor).HasMaxLength(254).IsRequired();
        movimiento.HasOne<ProductoInventarioHistorico>().WithMany().HasForeignKey(m => new { m.EmpresaId, m.ProductoId })
            .HasPrincipalKey(p => new { p.EmpresaId, p.Id }).OnDelete(DeleteBehavior.Restrict);
        movimiento.HasOne<PrestamoInventario>().WithMany().HasForeignKey(m => new { m.EmpresaId, m.PrestamoId })
            .HasPrincipalKey(p => new { p.EmpresaId, p.Id }).OnDelete(DeleteBehavior.Restrict);
        movimiento.HasIndex(m => new { m.EmpresaId, m.ProductoId, m.Id });
        movimiento.HasIndex(m => new { m.EmpresaId, m.SolicitudId }).IsUnique();

        var empresa = modelBuilder.Entity<Empresa>();
        empresa.ToTable("Empresa");
        empresa.Property(item => item.Nombre).HasMaxLength(140).IsRequired();
        empresa.Property(item => item.Telefono).HasMaxLength(25).IsRequired();
        empresa.Property(item => item.TelefonoOpcional).HasMaxLength(25);

        var usuario = modelBuilder.Entity<Usuario>();
        usuario.ToTable("Usuario");
        usuario.Property(item => item.Correo).HasMaxLength(254).IsRequired();
        usuario.Property(item => item.CorreoNormalizado).HasMaxLength(254).IsRequired();
        usuario.Property(item => item.ContraseniaHash).IsRequired();
        usuario.Property(item => item.Telefono).HasMaxLength(25);
        usuario.HasIndex(item => new { item.EmpresaId, item.CorreoNormalizado }).IsUnique();
        usuario.HasOne(item => item.Empresa).WithMany().HasForeignKey(item => item.EmpresaId)
            .OnDelete(DeleteBehavior.Cascade);
        usuario.HasOne(item => item.Rol).WithMany()
            .HasForeignKey(item => new { item.EmpresaId, item.RolId })
            .HasPrincipalKey(item => new { item.EmpresaId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);

        var rol = modelBuilder.Entity<Rol>();
        rol.ToTable("Rol");
        rol.Property(item => item.Nombre).HasMaxLength(80).IsRequired();
        rol.Property(item => item.NombreNormalizado).HasMaxLength(80).IsRequired();
        rol.Property(item => item.CodigoSistema).HasMaxLength(40);
        rol.HasIndex(item => new { item.EmpresaId, item.NombreNormalizado }).IsUnique();
        rol.HasIndex(item => new { item.EmpresaId, item.CodigoSistema }).IsUnique();
        rol.HasOne(item => item.Empresa).WithMany().HasForeignKey(item => item.EmpresaId)
            .OnDelete(DeleteBehavior.Cascade);

        var grupo = modelBuilder.Entity<GrupoTrabajo>();
        grupo.ToTable("GrupoTrabajo");
        grupo.Property(item => item.Nombre).HasMaxLength(100).IsRequired();
        grupo.Property(item => item.NombreNormalizado).HasMaxLength(100).IsRequired();
        grupo.HasIndex(item => new { item.EmpresaId, item.NombreNormalizado }).IsUnique();
        grupo.HasOne<Empresa>().WithMany().HasForeignKey(item => item.EmpresaId).OnDelete(DeleteBehavior.Cascade);
        grupo.HasOne(item => item.Supervisor).WithMany()
            .HasForeignKey(item => new { item.EmpresaId, item.SupervisorId })
            .HasPrincipalKey(item => new { item.EmpresaId, item.Id }).OnDelete(DeleteBehavior.Restrict);

        var miembro = modelBuilder.Entity<MiembroGrupo>();
        miembro.ToTable("MiembroGrupo");
        miembro.HasKey(item => new { item.EmpresaId, item.GrupoId, item.UsuarioId });
        miembro.HasOne(item => item.Grupo).WithMany(item => item.Miembros)
            .HasForeignKey(item => new { item.EmpresaId, item.GrupoId })
            .HasPrincipalKey(item => new { item.EmpresaId, item.Id }).OnDelete(DeleteBehavior.Cascade);
        miembro.HasOne(item => item.Usuario).WithMany()
            .HasForeignKey(item => new { item.EmpresaId, item.UsuarioId })
            .HasPrincipalKey(item => new { item.EmpresaId, item.Id }).OnDelete(DeleteBehavior.Cascade);

        var estado = modelBuilder.Entity<EstadoTarea>();
        estado.ToTable("EstadoTarea");
        estado.Property(item => item.Nombre).HasMaxLength(80).IsRequired();
        estado.Property(item => item.NombreNormalizado).HasMaxLength(80).IsRequired();
        estado.Property(item => item.Color).HasMaxLength(7).IsRequired();
        estado.HasIndex(item => new { item.EmpresaId, item.GrupoId, item.NombreNormalizado }).IsUnique();
        estado.HasIndex(item => new { item.EmpresaId, item.GrupoId }).IsUnique().HasFilter("\"EsPredeterminado\"");
        estado.HasOne(item => item.Grupo).WithMany(item => item.Estados)
            .HasForeignKey(item => new { item.EmpresaId, item.GrupoId })
            .HasPrincipalKey(item => new { item.EmpresaId, item.Id }).OnDelete(DeleteBehavior.Cascade);

        var tarea = modelBuilder.Entity<Tarea>();
        tarea.ToTable("Tarea", table =>
        {
            table.HasCheckConstraint("CK_Tarea_Autoasignacion", "\"AsignadoAId\" IS NULL OR NOT \"PermitirAutoasignacion\"");
        });
        tarea.Property(item => item.Titulo).HasMaxLength(140).IsRequired();
        tarea.Property(item => item.Descripcion).HasMaxLength(4000).IsRequired();
        tarea.Property(item => item.Version).IsConcurrencyToken();
        tarea.HasOne<Empresa>().WithMany().HasForeignKey(item => item.EmpresaId).OnDelete(DeleteBehavior.Cascade);
        tarea.HasOne(item => item.Grupo).WithMany()
            .HasForeignKey(item => new { item.EmpresaId, item.GrupoId })
            .HasPrincipalKey(item => new { item.EmpresaId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        tarea.HasOne(item => item.Estado).WithMany()
            .HasForeignKey(item => new { item.EmpresaId, item.GrupoId, item.EstadoId })
            .HasPrincipalKey(item => new { item.EmpresaId, item.GrupoId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        tarea.HasOne(item => item.AsignadoA).WithMany()
            .HasForeignKey(item => new { item.EmpresaId, item.AsignadoAId })
            .HasPrincipalKey(item => new { item.EmpresaId, item.Id }).OnDelete(DeleteBehavior.Restrict);

        var nota = modelBuilder.Entity<NotaTarea>();
        nota.ToTable("NotaTarea", table => table.HasCheckConstraint("CK_NotaTarea_TipoAutor", "\"TipoAutor\" IN ('Asignado', 'Supervisor')"));
        nota.Property(item => item.Titulo).HasMaxLength(140).IsRequired();
        nota.Property(item => item.Descripcion).HasMaxLength(4000).IsRequired();
        nota.Property(item => item.CorreoAutor).HasMaxLength(254).IsRequired();
        nota.Property(item => item.TipoAutor).HasMaxLength(20).IsRequired();
        nota.HasOne(item => item.Tarea).WithMany()
            .HasForeignKey(item => new { item.EmpresaId, item.TareaId })
            .HasPrincipalKey(item => new { item.EmpresaId, item.Id }).OnDelete(DeleteBehavior.Cascade);
        nota.HasOne(item => item.Autor).WithMany()
            .HasForeignKey(item => new { item.EmpresaId, item.AutorId })
            .HasPrincipalKey(item => new { item.EmpresaId, item.Id }).OnDelete(DeleteBehavior.Restrict);

        var auditoria = modelBuilder.Entity<RegistroAuditoria>();
        auditoria.ToTable("RegistroAuditoria");
        auditoria.Property(item => item.CorreoAutor).HasMaxLength(254).IsRequired();
        auditoria.Property(item => item.Accion).HasMaxLength(20).IsRequired();
        auditoria.Property(item => item.Entidad).HasMaxLength(40).IsRequired();
        auditoria.Property(item => item.EntidadId).HasMaxLength(80).IsRequired();
        auditoria.Property(item => item.Detalle).HasMaxLength(600).IsRequired();
        auditoria.HasIndex(item => new { item.EmpresaId, item.Id });
        auditoria.HasOne<Empresa>().WithMany().HasForeignKey(item => item.EmpresaId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<DataProtectionKey>().ToTable("data_protection_keys");
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ChangeTracker.DetectChanges();
        var cambios = ChangeTracker.Entries().Where(AuditoriaCambios.EsCambio)
            .Select(AuditoriaCambios.Capturar).ToArray();
        if (cambios.Length == 0) return await base.SaveChangesAsync(cancellationToken);
        await using var transaction = Database.CurrentTransaction is null
            ? await Database.BeginTransactionAsync(cancellationToken) : null;
        var resultado = await base.SaveChangesAsync(cancellationToken);
        Auditoria.AddRange(cambios.Select(item => item.Crear(accessor?.HttpContext, cambios)));
        await base.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return resultado;
    }
}
