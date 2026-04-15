namespace ErpBoulder.Api.Controllers.Administracion;

using ErpBoulder.Application.DTOs.Administracion;
using ErpBoulder.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize(Roles = "ADMIN_TOTAL,ADMIN_EMPRESA")]
[Route("api/administracion/clientes")]
public sealed class ClientesController(IAdministracionService administracionService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ClienteDto>>> Get([FromQuery] string? search, CancellationToken cancellationToken)
    {
        return Ok(await administracionService.GetClientesAsync(search, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<ClienteDto>> Post([FromBody] UpsertClienteRequestDto request, CancellationToken cancellationToken)
    {
        return Ok(await administracionService.CreateClienteAsync(request, cancellationToken));
    }
}
