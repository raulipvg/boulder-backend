namespace ErpBoulder.Application.Interfaces.Services;

using ErpBoulder.Application.DTOs.Ventas;

public interface IVentasService
{
    Task<IReadOnlyCollection<PosCatalogItemDto>> GetPosCatalogAsync(CancellationToken cancellationToken);
    Task<VentaPreviewDto> PreviewVentaAsync(PreviewVentaRequestDto request, CancellationToken cancellationToken);
    Task<VentaDto> CreateVentaAsync(CreateVentaRequestDto request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<VentaResumenDto>> GetVentasAsync(string? estado, CancellationToken cancellationToken);
    Task<VentaDto> GetVentaAsync(long ventaId, CancellationToken cancellationToken);
    Task<VentaDto> AnularVentaAsync(long ventaId, CancelVentaRequestDto request, CancellationToken cancellationToken);
}
