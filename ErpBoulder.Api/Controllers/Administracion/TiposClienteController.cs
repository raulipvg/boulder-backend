namespace ErpBoulder.Api.Controllers.Administracion;

using ErpBoulder.Application.DTOs.Administracion;
using ErpBoulder.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize(Roles = "ADMIN_TOTAL,ADMIN_EMPRESA,VENDEDOR_EMPRESA")]
[Route("api/administracion/tipos-cliente")]
public sealed class TiposClienteController(IAdministracionService administracionService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<TipoClienteDto>>> Get(CancellationToken cancellationToken)
    {
        return Ok(await administracionService.GetTiposClienteAsync(cancellationToken));
    }

    [Authorize(Roles = "ADMIN_TOTAL,ADMIN_EMPRESA")]
    [HttpPost]
    public async Task<ActionResult<TipoClienteDto>> Post([FromBody] CreateTipoClienteRequestDto request, CancellationToken cancellationToken)
    {
        return Ok(await administracionService.CreateTipoClienteAsync(request, cancellationToken));
    }
}
