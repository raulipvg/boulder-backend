namespace ErpBoulder.Api.Controllers.Ventas;

using ErpBoulder.Application.DTOs.Ventas;
using ErpBoulder.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize(Roles = "ADMIN_TOTAL,ADMIN_EMPRESA,VENDEDOR_EMPRESA")]
[Route("api/ventas/pos")]
public sealed class PosController(IVentasService ventasService) : ControllerBase
{
    [HttpGet("catalogo")]
    public async Task<ActionResult<IReadOnlyCollection<PosCatalogItemDto>>> GetCatalogo(CancellationToken cancellationToken)
    {
        return Ok(await ventasService.GetPosCatalogAsync(cancellationToken));
    }

    [HttpPost("preview")]
    public async Task<ActionResult<VentaPreviewDto>> PreviewVenta([FromBody] PreviewVentaRequestDto request, CancellationToken cancellationToken)
    {
        return Ok(await ventasService.PreviewVentaAsync(request, cancellationToken));
    }

    [HttpPost("ventas")]
    public async Task<ActionResult<VentaDto>> PostVenta([FromBody] CreateVentaRequestDto request, CancellationToken cancellationToken)
    {
        return Ok(await ventasService.CreateVentaAsync(request, cancellationToken));
    }
}
