namespace ErpBoulder.Api.Controllers.Administracion;

using ErpBoulder.Application.DTOs.Administracion;
using ErpBoulder.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize]
[Route("api/administracion/clientes")]
public sealed class ClientesController(IAdministracionService administracionService) : ControllerBase
{
    [Authorize(Roles = "ADMIN_TOTAL,ADMIN_EMPRESA")]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ClienteDto>>> Get([FromQuery] string? search, CancellationToken cancellationToken)
    {
        return Ok(await administracionService.GetClientesAsync(search, cancellationToken));
    }

    [Authorize(Roles = "ADMIN_TOTAL,ADMIN_EMPRESA,VENDEDOR_EMPRESA")]
    [HttpPost]
    public async Task<ActionResult<ClienteDto>> Post([FromBody] UpsertClienteRequestDto request, CancellationToken cancellationToken)
    {
        return Ok(await administracionService.CreateClienteAsync(request, cancellationToken));
    }

    [Authorize(Roles = "ADMIN_TOTAL,ADMIN_EMPRESA")]
    [HttpPut("{clienteEmpresaId:long}")]
    public async Task<ActionResult<ClienteDto>> Put(long clienteEmpresaId, [FromBody] UpsertClienteRequestDto request, CancellationToken cancellationToken)
    {
        return Ok(await administracionService.UpdateClienteAsync(clienteEmpresaId, request, cancellationToken));
    }
}
