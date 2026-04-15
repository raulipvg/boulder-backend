namespace ErpBoulder.Application.Interfaces.Services;

using ErpBoulder.Application.DTOs.Administracion;

public interface IAdministracionService
{
    Task<IReadOnlyCollection<LookupDto>> GetTiposProductoBaseAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<LookupDto>> GetMediosPagoAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<LookupDto>> GetBloquesHorariosAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<LookupDto>> GetProfesoresAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<EmpresaDto>> GetEmpresasAsync(CancellationToken cancellationToken);
    Task<EmpresaDto> CreateEmpresaAsync(CreateEmpresaRequestDto request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<UsuarioDto>> GetUsuariosAsync(CancellationToken cancellationToken);
    Task<UsuarioDto> CreateUsuarioAsync(CreateUsuarioRequestDto request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<TipoClienteDto>> GetTiposClienteAsync(CancellationToken cancellationToken);
    Task<TipoClienteDto> CreateTipoClienteAsync(CreateTipoClienteRequestDto request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ClienteDto>> GetClientesAsync(string? search, CancellationToken cancellationToken);
    Task<ClienteDto> CreateClienteAsync(UpsertClienteRequestDto request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ProductoDto>> GetProductosAsync(CancellationToken cancellationToken);
    Task<ProductoDto> CreateProductoAsync(UpsertProductoRequestDto request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<TarifaDto>> GetTarifasAsync(CancellationToken cancellationToken);
    Task<TarifaDto> CreateTarifaAsync(UpsertTarifaRequestDto request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ClaseDto>> GetClasesAsync(CancellationToken cancellationToken);
    Task<ClaseDto> CreateClaseAsync(UpsertClaseRequestDto request, CancellationToken cancellationToken);
}
