namespace ErpBoulder.Api.Controllers.Administracion;

using ErpBoulder.Application.DTOs.Administracion;
using ErpBoulder.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize(Roles = "ADMIN_TOTAL,ADMIN_EMPRESA")]
[Route("api/administracion/productos")]
public sealed class ProductosController(IAdministracionService administracionService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ProductoDto>>> Get(CancellationToken cancellationToken)
    {
        return Ok(await administracionService.GetProductosAsync(cancellationToken));
    }

    [HttpGet("{productoEmpresaId:long}/tarifas")]
    public async Task<ActionResult<IReadOnlyCollection<TarifaProductoResumenDto>>> GetTarifasByProducto(long productoEmpresaId, CancellationToken cancellationToken)
    {
        return Ok(await administracionService.GetTarifasByProductoAsync(productoEmpresaId, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<ProductoDto>> Post([FromBody] UpsertProductoRequestDto request, CancellationToken cancellationToken)
    {
        return Ok(await administracionService.CreateProductoAsync(request, cancellationToken));
    }

    [HttpPut("{productoEmpresaId:long}")]
    public async Task<ActionResult<ProductoDto>> Put(long productoEmpresaId, [FromBody] UpsertProductoRequestDto request, CancellationToken cancellationToken)
    {
        return Ok(await administracionService.UpdateProductoAsync(productoEmpresaId, request, cancellationToken));
    }
}
