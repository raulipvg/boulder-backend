namespace ErpBoulder.Api.Controllers.Administracion;

using ErpBoulder.Application.DTOs.Administracion;
using ErpBoulder.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize(Roles = "ADMIN_TOTAL,ADMIN_EMPRESA")]
[Route("api/administracion/usuarios")]
public sealed class UsuariosController(IAdministracionService administracionService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<UsuarioDto>>> Get(CancellationToken cancellationToken)
    {
        return Ok(await administracionService.GetUsuariosAsync(cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<UsuarioDto>> Post([FromBody] CreateUsuarioRequestDto request, CancellationToken cancellationToken)
    {
        return Ok(await administracionService.CreateUsuarioAsync(request, cancellationToken));
    }

    [HttpPut("{usuarioId:long}")]
    public async Task<ActionResult<UsuarioDto>> Put(long usuarioId, [FromBody] UpdateUsuarioRequestDto request, CancellationToken cancellationToken)
    {
        return Ok(await administracionService.UpdateUsuarioAsync(usuarioId, request, cancellationToken));
    }

    [HttpPut("{usuarioId:long}/password")]
    public async Task<ActionResult> ChangePassword(long usuarioId, [FromBody] ChangeUsuarioPasswordRequestDto request, CancellationToken cancellationToken)
    {
        await administracionService.ChangeUsuarioPasswordAsync(usuarioId, request, cancellationToken);
        return NoContent();
    }
}
