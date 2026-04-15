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
    public async Task<ActionResult<DashboardReportDto>> Dashboard(CancellationToken cancellationToken)
    {
        return Ok(await reportesService.GetDashboardAsync(cancellationToken));
    }

    [HttpGet("ventas/producto")]
    public async Task<ActionResult<IReadOnlyCollection<SimpleReportItemDto>>> VentasPorProducto(CancellationToken cancellationToken)
    {
        return Ok(await reportesService.GetVentasPorProductoAsync(cancellationToken));
    }

    [HttpGet("ventas/tipo-cliente")]
    public async Task<ActionResult<IReadOnlyCollection<SimpleReportItemDto>>> VentasPorTipoCliente(CancellationToken cancellationToken)
    {
        return Ok(await reportesService.GetVentasPorTipoClienteAsync(cancellationToken));
    }

    [HttpGet("accesos/bloque")]
    public async Task<ActionResult<IReadOnlyCollection<SimpleReportItemDto>>> AccesosPorBloque(CancellationToken cancellationToken)
    {
        return Ok(await reportesService.GetAccesosPorBloqueAsync(cancellationToken));
    }

    [HttpGet("clases/uso")]
    public async Task<ActionResult<IReadOnlyCollection<SimpleReportItemDto>>> UsoClases(CancellationToken cancellationToken)
    {
        return Ok(await reportesService.GetUsoClasesAsync(cancellationToken));
    }
}
