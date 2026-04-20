namespace ErpBoulder.Application.DTOs.Reportes;

public sealed record DashboardReportDto(
    decimal VentasTotales,
    int VentasEmitidas,
    int ClientesActivos,
    int MensualidadesActivas,
    int PacksVigentes,
    int AccesosAutorizadosHoy);

public sealed record SimpleReportItemDto(string Etiqueta, decimal Valor);

public sealed record VentaReporteExportDto(
    long VentaId,
    long VentaDetalleId,
    string NumeroComprobante,
    DateTimeOffset FechaHoraVenta,
    string? ClienteNombre,
    string? ClienteRut,
    string? TipoCliente,
    string VendedorNombre,
    string ProductoNombre,
    int Cantidad,
    decimal PrecioUnitario,
    decimal SubtotalDetalle,
    decimal TotalVenta,
    string EstadoVenta);

public sealed record AccesoReporteExportDto(
    long AccesoEventoId,
    DateTimeOffset FechaHoraAcceso,
    string Resultado,
    string? MotivoRechazo,
    string ClienteNombre,
    string ClienteRut,
    string TipoCliente,
    string? ProductoNombre,
    string? BloqueHorario,
    string UsuarioValidador);

public sealed record ClaseReporteExportDto(
    long ClaseAsistenciaId,
    DateTimeOffset FechaHoraRegistro,
    DateOnly FechaSesion,
    TimeOnly HoraInicioSesion,
    TimeOnly HoraFinSesion,
    string ClaseNombre,
    string ProfesorNombre,
    string ClienteNombre,
    string ClienteRut,
    string TipoCliente,
    string ProductoBeneficio,
    string EstadoAsistencia);
