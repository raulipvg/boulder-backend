namespace ErpBoulder.Api.Controllers.Administracion;

using ErpBoulder.Application.DTOs.Administracion;
using ErpBoulder.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize(Roles = "ADMIN_TOTAL,ADMIN_EMPRESA")]
[Route("api/administracion/clases")]
public sealed class ClasesController(IAdministracionService administracionService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ClaseAgendaDto>>> Get([FromQuery] bool? activo, CancellationToken cancellationToken)
    {
        return Ok(await administracionService.GetClasesAsync(activo, cancellationToken));
    }

    [HttpGet("{claseId:long}")]
    public async Task<ActionResult<ClaseDto>> GetById(long claseId, CancellationToken cancellationToken)
    {
        return Ok(await administracionService.GetClaseByIdAsync(claseId, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<ClaseDto>> Post([FromBody] UpsertClaseRequestDto request, CancellationToken cancellationToken)
    {
        return Ok(await administracionService.CreateClaseAsync(request, cancellationToken));
    }

    [HttpPut("{claseId:long}")]
    public async Task<ActionResult<ClaseDto>> Put(long claseId, [FromBody] UpsertClaseRequestDto request, CancellationToken cancellationToken)
    {
        return Ok(await administracionService.UpdateClaseAsync(claseId, request, cancellationToken));
    }
}
