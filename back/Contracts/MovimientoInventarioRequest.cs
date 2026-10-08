using System.ComponentModel.DataAnnotations;

namespace back.Contracts;

public sealed record MovimientoInventarioRequest(
    [Range(1, int.MaxValue)] int ProductoId,
    [Range(1, long.MaxValue)] long Version,
    [Required, MaxLength(30)] string Tipo,
    [Range(typeof(decimal), "0", "999999999999.999")] decimal Cantidad,
    [Required, MaxLength(500)] string Motivo,
    Guid SolicitudId,
    [MaxLength(100)] string? Referencia = null,
    bool VentaDetallada = false,
    [MaxLength(254)] string? Cliente = null,
    [Range(typeof(decimal), "0", "9999999999")] decimal? PrecioUnitario = null,
    int? DestinatarioUsuarioId = null,
    [MaxLength(254)] string? DestinatarioExterno = null,
    DateOnly? FechaPrevista = null,
    int? PrestamoId = null,
    [Range(1, int.MaxValue)] int? BodegaId = null);

public sealed record TrasladoInventarioRequest(
    [Range(1, int.MaxValue)] int ProductoId,
    [Range(1, long.MaxValue)] long Version,
    [Range(1, int.MaxValue)] int OrigenId,
    [Range(1, int.MaxValue)] int DestinoId,
    [Range(typeof(decimal), "0", "999999999999.999")] decimal Cantidad,
    [Required, MaxLength(500)] string Motivo,
    Guid SolicitudId,
    [MaxLength(100)] string? Referencia = null,
    [Range(1, int.MaxValue)] int ProductoDestinoId = 0,
    [Range(1, long.MaxValue)] long VersionDestino = 0);

public sealed record CatalogoInventarioRequest([Required, MaxLength(100)] string Nombre, bool Activa = true);
