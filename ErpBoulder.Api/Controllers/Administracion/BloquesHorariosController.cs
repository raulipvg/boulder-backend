namespace ErpBoulder.Api.Controllers.Administracion;

using ErpBoulder.Application.DTOs.Administracion;
using ErpBoulder.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/administracion/bloques-horarios")]
[Authorize]
public sealed class BloquesHorariosController(IAdministracionService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        => Ok(await service.GetBloquesHorariosComercialesAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertBloqueHorarioRequestDto request, CancellationToken cancellationToken)
        => Ok(await service.CreateBloqueHorarioAsync(request, cancellationToken));

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpsertBloqueHorarioRequestDto request, CancellationToken cancellationToken)
        => Ok(await service.UpdateBloqueHorarioAsync(id, request, cancellationToken));
}
