namespace ErpBoulder.Api.Controllers.Operacion;

using ErpBoulder.Application.DTOs.Operacion;
using ErpBoulder.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize(Roles = "ADMIN_TOTAL,ADMIN_EMPRESA,VENDEDOR_EMPRESA")]
[Route("api/operacion/accesos")]
public sealed class AccesosController(IOperacionService operacionService) : ControllerBase
{
    [HttpGet("clientes")]
    public async Task<ActionResult<IReadOnlyCollection<ClienteLookupDto>>> BuscarClientes([FromQuery] string? search, CancellationToken cancellationToken)
    {
        return Ok(await operacionService.BuscarClientesAsync(search, cancellationToken));
    }

    [HttpGet("preview/{clienteEmpresaId:long}")]
    public async Task<ActionResult<AccessPreviewDto>> Preview(long clienteEmpresaId, CancellationToken cancellationToken)
    {
        return Ok(await operacionService.PrevisualizarAccesoAsync(clienteEmpresaId, cancellationToken));
    }

    [HttpPost("validar")]
    public async Task<ActionResult<AccessValidationResultDto>> Validar([FromBody] ValidateAccessRequestDto request, CancellationToken cancellationToken)
    {
        return Ok(await operacionService.ValidarAccesoAsync(request, cancellationToken));
    }
}
