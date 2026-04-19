namespace ErpBoulder.Application.DTOs.Ventas;

public sealed record PosCatalogItemDto(
    long ProductoEmpresaId,
    string NombreComercial,
    string TipoProductoBaseCodigo,
    string ModoPrecio,
    decimal? PrecioFijo,
    bool RequiereCliente,
    bool GeneraBeneficio,
    bool VisiblePos,
    decimal? TarifaGeneralVigente,
    decimal? TarifaEstudianteVigente,
    string? TarifaGeneralBloqueHorario,
    string? TarifaEstudianteBloqueHorario,
    IReadOnlyCollection<string> DiasClase);

public sealed record VentaItemRequestDto(long ProductoEmpresaId, int Cantidad, long? ClienteEmpresaIdAsignado, DateOnly? FechaInicioVigencia, string? Observacion);

public sealed record VentaPagoRequestDto(long MedioPagoId, decimal Monto, string? Referencia);

public sealed record PreviewVentaRequestDto(long? ClienteEmpresaId, IReadOnlyCollection<VentaItemRequestDto> Items);

public sealed record VentaPreviewDetalleDto(long ProductoEmpresaId, string ProductoNombre, int Cantidad, decimal PrecioUnitario, decimal Subtotal);

public sealed record VentaPreviewDto(decimal Subtotal, decimal Total, IReadOnlyCollection<VentaPreviewDetalleDto> Detalles);

public sealed record CreateVentaRequestDto(long? ClienteEmpresaId, IReadOnlyCollection<VentaItemRequestDto> Items, IReadOnlyCollection<VentaPagoRequestDto> Pagos);

public sealed record VentaPagoDto(long VentaPagoId, string MedioPago, decimal Monto, string? Referencia);

public sealed record VentaDetalleDto(long VentaDetalleId, long ProductoEmpresaId, string ProductoNombre, int Cantidad, decimal PrecioUnitario, decimal Subtotal, DateOnly? FechaInicioVigencia, long? BeneficioClienteId);

public sealed record VentaResumenDto(long VentaId, string NumeroComprobante, DateTimeOffset FechaHora, string Estado, decimal Total, string? ClienteNombre, string? MotivoAnulacion);

public sealed record VentaDto(long VentaId, string NumeroComprobante, DateTimeOffset FechaHora, string Estado, decimal Subtotal, decimal Descuento, decimal Total, long? ClienteEmpresaId, string? ClienteNombre, IReadOnlyCollection<VentaDetalleDto> Detalles, IReadOnlyCollection<VentaPagoDto> Pagos, string? MotivoAnulacion);

public sealed record CancelVentaRequestDto(string Motivo);
