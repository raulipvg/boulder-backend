namespace ErpBoulder.Application.Interfaces.Services;

using ErpBoulder.Application.DTOs.Reportes;

public interface IReportesService
{
    Task<DashboardReportDto> GetDashboardAsync(string? periodo, DateOnly? fechaReferencia, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<SimpleReportItemDto>> GetVentasPorProductoAsync(string? periodo, DateOnly? fechaReferencia, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<SimpleReportItemDto>> GetVentasPorTipoClienteAsync(string? periodo, DateOnly? fechaReferencia, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<SimpleReportItemDto>> GetAccesosPorBloqueAsync(string? periodo, DateOnly? fechaReferencia, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<SimpleReportItemDto>> GetUsoClasesAsync(string? periodo, DateOnly? fechaReferencia, CancellationToken cancellationToken);
}
