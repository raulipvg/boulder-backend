namespace ErpBoulder.Application.Interfaces.Services;

using ErpBoulder.Application.DTOs.Ventas;

public interface IVentasService
{
    Task<IReadOnlyCollection<PosCatalogItemDto>> GetPosCatalogAsync(CancellationToken cancellationToken);
    Task<VentaPreviewDto> PreviewVentaAsync(PreviewVentaRequestDto request, CancellationToken cancellationToken);
    Task<VentaDto> CreateVentaAsync(CreateVentaRequestDto request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<VentaDto>> GetVentasAsync(CancellationToken cancellationToken);
    Task<VentaDto> AnularVentaAsync(long ventaId, CancelVentaRequestDto request, CancellationToken cancellationToken);
}
