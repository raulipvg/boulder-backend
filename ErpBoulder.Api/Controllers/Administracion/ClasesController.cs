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
    public async Task<ActionResult<IReadOnlyCollection<ClaseDto>>> Get(CancellationToken cancellationToken)
    {
        return Ok(await administracionService.GetClasesAsync(cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<ClaseDto>> Post([FromBody] UpsertClaseRequestDto request, CancellationToken cancellationToken)
    {
        return Ok(await administracionService.CreateClaseAsync(request, cancellationToken));
    }
}
