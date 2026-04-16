namespace ErpBoulder.Api.Controllers.Administracion;

using ErpBoulder.Application.DTOs.Administracion;
using ErpBoulder.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize(Roles = "ADMIN_TOTAL,ADMIN_EMPRESA")]
[Route("api/administracion/tarifas")]
public sealed class TarifasController(IAdministracionService administracionService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<TarifaDto>>> Get([FromQuery] string? tipoClienteCodigo, CancellationToken cancellationToken)
    {
        return Ok(await administracionService.GetTarifasAsync(tipoClienteCodigo, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<TarifaDto>> Post([FromBody] UpsertTarifaRequestDto request, CancellationToken cancellationToken)
    {
        return Ok(await administracionService.CreateTarifaAsync(request, cancellationToken));
    }

    [HttpPut("{tarifaProductoId:long}")]
    public async Task<ActionResult<TarifaDto>> Put(long tarifaProductoId, [FromBody] UpsertTarifaRequestDto request, CancellationToken cancellationToken)
    {
        return Ok(await administracionService.UpdateTarifaAsync(tarifaProductoId, request, cancellationToken));
    }
}
