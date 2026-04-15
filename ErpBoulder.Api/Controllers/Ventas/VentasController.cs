namespace ErpBoulder.Api.Controllers.Ventas;

using ErpBoulder.Application.DTOs.Ventas;
using ErpBoulder.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize(Roles = "ADMIN_TOTAL,ADMIN_EMPRESA,VENDEDOR_EMPRESA")]
[Route("api/ventas/ventas")]
public sealed class VentasController(IVentasService ventasService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<VentaDto>>> Get(CancellationToken cancellationToken)
    {
        return Ok(await ventasService.GetVentasAsync(cancellationToken));
    }

    [HttpPost("{ventaId:long}/anular")]
    public async Task<ActionResult<VentaDto>> Anular(long ventaId, [FromBody] CancelVentaRequestDto request, CancellationToken cancellationToken)
    {
        return Ok(await ventasService.AnularVentaAsync(ventaId, request, cancellationToken));
    }
}
