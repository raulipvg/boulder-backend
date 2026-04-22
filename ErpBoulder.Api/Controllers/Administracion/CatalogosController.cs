namespace ErpBoulder.Api.Controllers.Administracion;

using ErpBoulder.Application.DTOs.Administracion;
using ErpBoulder.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize]
[Route("api/administracion/catalogos")]
public sealed class CatalogosController(IAdministracionService administracionService) : ControllerBase
{
    [HttpGet("tipos-producto-base")]
    public async Task<ActionResult<IReadOnlyCollection<LookupDto>>> GetTiposProductoBase(CancellationToken cancellationToken)
    {
        return Ok(await administracionService.GetTiposProductoBaseAsync(cancellationToken));
    }

    [HttpGet("tipos-cliente")]
    public async Task<ActionResult<IReadOnlyCollection<LookupDto>>> GetTiposCliente(CancellationToken cancellationToken)
    {
        return Ok(await administracionService.GetTiposClienteCatalogoAsync(cancellationToken));
    }

    [HttpGet("medios-pago")]
    public async Task<ActionResult<IReadOnlyCollection<LookupDto>>> GetMediosPago(CancellationToken cancellationToken)
    {
        return Ok(await administracionService.GetMediosPagoAsync(cancellationToken));
    }

    [HttpGet("bloques")]
    public async Task<ActionResult<IReadOnlyCollection<LookupDto>>> GetBloques(CancellationToken cancellationToken)
    {
        return Ok(await administracionService.GetBloquesHorariosAsync(cancellationToken));
    }

    [HttpGet("bloques-lite")]
    public async Task<ActionResult<IReadOnlyCollection<IdNombreDto>>> GetBloquesLite(CancellationToken cancellationToken)
    {
        return Ok(await administracionService.GetBloquesHorariosCatalogoLiteAsync(cancellationToken));
    }

    [HttpGet("productos")]
    public async Task<ActionResult<IReadOnlyCollection<IdNombreDto>>> GetProductos(CancellationToken cancellationToken)
    {
        return Ok(await administracionService.GetProductosCatalogoAsync(cancellationToken));
    }

    [HttpGet("profesores")]
    public async Task<ActionResult<IReadOnlyCollection<IdNombreDto>>> GetProfesores(CancellationToken cancellationToken)
    {
        return Ok(await administracionService.GetProfesoresAsync(cancellationToken));
    }

    [HttpGet("clases")]
    public async Task<ActionResult<IReadOnlyCollection<ClaseCatalogoDto>>> GetClases(CancellationToken cancellationToken)
    {
        return Ok(await administracionService.GetClasesCatalogoAsync(cancellationToken));
    }
}
