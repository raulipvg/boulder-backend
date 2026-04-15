namespace ErpBoulder.Api.Controllers.Operacion;

using ErpBoulder.Application.DTOs.Operacion;
using ErpBoulder.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize(Roles = "ADMIN_TOTAL,ADMIN_EMPRESA,VENDEDOR_EMPRESA")]
[Route("api/operacion/clases")]
public sealed class ClasesController(IOperacionService operacionService) : ControllerBase
{
    [HttpGet("sesiones")]
    public async Task<ActionResult<IReadOnlyCollection<ClaseSesionDto>>> GetSesiones([FromQuery] DateOnly? fecha, CancellationToken cancellationToken)
    {
        return Ok(await operacionService.GetSesionesAsync(fecha, cancellationToken));
    }

    [HttpPost("asistencias")]
    public async Task<ActionResult<ClaseAsistenciaDto>> RegistrarAsistencia([FromBody] RegisterAttendanceRequestDto request, CancellationToken cancellationToken)
    {
        return Ok(await operacionService.RegistrarAsistenciaAsync(request, cancellationToken));
    }
}
