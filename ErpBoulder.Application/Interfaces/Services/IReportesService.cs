namespace ErpBoulder.Application.Interfaces.Services;

using ErpBoulder.Application.DTOs.Reportes;

public interface IReportesService
{
    Task<DashboardReportDto> GetDashboardAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<SimpleReportItemDto>> GetVentasPorProductoAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<SimpleReportItemDto>> GetVentasPorTipoClienteAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<SimpleReportItemDto>> GetAccesosPorBloqueAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<SimpleReportItemDto>> GetUsoClasesAsync(CancellationToken cancellationToken);
}
