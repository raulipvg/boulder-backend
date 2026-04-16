namespace ErpBoulder.Api.Controllers.Administracion;

using ErpBoulder.Application.DTOs.Administracion;
using ErpBoulder.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize]
[Route("api/administracion/empresas")]
public sealed class EmpresasController(IAdministracionService administracionService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<EmpresaDto>>> Get(CancellationToken cancellationToken)
    {
        return Ok(await administracionService.GetEmpresasAsync(cancellationToken));
    }

    [Authorize(Roles = "ADMIN_TOTAL")]
    [HttpPost]
    public async Task<ActionResult<EmpresaDto>> Post([FromBody] CreateEmpresaRequestDto request, CancellationToken cancellationToken)
    {
        return Ok(await administracionService.CreateEmpresaAsync(request, cancellationToken));
    }

    [Authorize(Roles = "ADMIN_TOTAL")]
    [HttpPut("{empresaId:long}")]
    public async Task<ActionResult<EmpresaDto>> Put(long empresaId, [FromBody] CreateEmpresaRequestDto request, CancellationToken cancellationToken)
    {
        return Ok(await administracionService.UpdateEmpresaAsync(empresaId, request, cancellationToken));
    }
}
