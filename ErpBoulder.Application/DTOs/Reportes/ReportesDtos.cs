namespace ErpBoulder.Application.DTOs.Reportes;

public sealed record DashboardReportDto(
    decimal VentasTotales,
    int VentasEmitidas,
    int ClientesActivos,
    int MensualidadesActivas,
    int PacksVigentes,
    int AccesosAutorizadosHoy);

public sealed record SimpleReportItemDto(string Etiqueta, decimal Valor);
