namespace ErpBoulder.Api.Controllers.Reportes;

using ErpBoulder.Application.DTOs.Reportes;
using ErpBoulder.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize(Roles = "ADMIN_TOTAL,ADMIN_EMPRESA")]
[Route("api/reportes")]
public sealed class ReportesController(IReportesService reportesService) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardReportDto>> Dashboard([FromQuery] string? periodo, [FromQuery] DateOnly? fechaReferencia, CancellationToken cancellationToken)
    {
        return Ok(await reportesService.GetDashboardAsync(periodo, fechaReferencia, cancellationToken));
    }

    [HttpGet("ventas/producto")]
    public async Task<ActionResult<IReadOnlyCollection<SimpleReportItemDto>>> VentasPorProducto([FromQuery] string? periodo, [FromQuery] DateOnly? fechaReferencia, CancellationToken cancellationToken)
    {
        return Ok(await reportesService.GetVentasPorProductoAsync(periodo, fechaReferencia, cancellationToken));
    }

    [HttpGet("ventas/tipo-cliente")]
    public async Task<ActionResult<IReadOnlyCollection<SimpleReportItemDto>>> VentasPorTipoCliente([FromQuery] string? periodo, [FromQuery] DateOnly? fechaReferencia, CancellationToken cancellationToken)
    {
        return Ok(await reportesService.GetVentasPorTipoClienteAsync(periodo, fechaReferencia, cancellationToken));
    }

    [HttpGet("accesos/bloque")]
    public async Task<ActionResult<IReadOnlyCollection<SimpleReportItemDto>>> AccesosPorBloque([FromQuery] string? periodo, [FromQuery] DateOnly? fechaReferencia, CancellationToken cancellationToken)
    {
        return Ok(await reportesService.GetAccesosPorBloqueAsync(periodo, fechaReferencia, cancellationToken));
    }

    [HttpGet("clases/uso")]
    public async Task<ActionResult<IReadOnlyCollection<SimpleReportItemDto>>> UsoClases([FromQuery] string? periodo, [FromQuery] DateOnly? fechaReferencia, CancellationToken cancellationToken)
    {
        return Ok(await reportesService.GetUsoClasesAsync(periodo, fechaReferencia, cancellationToken));
    }
}
