namespace ErpBoulder.Application.Interfaces.Services;

using ErpBoulder.Application.DTOs.Administracion;

public interface IAdministracionService
{
    Task<IReadOnlyCollection<LookupDto>> GetTiposProductoBaseAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<LookupDto>> GetMediosPagoAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<LookupDto>> GetBloquesHorariosAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<BloqueHorarioDto>> GetBloquesHorariosComercialesAsync(CancellationToken cancellationToken);
    Task<BloqueHorarioDto> CreateBloqueHorarioAsync(UpsertBloqueHorarioRequestDto request, CancellationToken cancellationToken);
    Task<BloqueHorarioDto> UpdateBloqueHorarioAsync(long bloqueHorarioComercialId, UpsertBloqueHorarioRequestDto request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<IdNombreDto>> GetProfesoresAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<EmpresaDto>> GetEmpresasAsync(CancellationToken cancellationToken);
    Task<EmpresaDto> CreateEmpresaAsync(CreateEmpresaRequestDto request, CancellationToken cancellationToken);
    Task<EmpresaDto> UpdateEmpresaAsync(long empresaId, CreateEmpresaRequestDto request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<UsuarioDto>> GetUsuariosAsync(CancellationToken cancellationToken);
    Task<UsuarioDto> CreateUsuarioAsync(CreateUsuarioRequestDto request, CancellationToken cancellationToken);
    Task<UsuarioDto> UpdateUsuarioAsync(long usuarioId, UpdateUsuarioRequestDto request, CancellationToken cancellationToken);
    Task ChangeUsuarioPasswordAsync(long usuarioId, ChangeUsuarioPasswordRequestDto request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<TipoClienteDto>> GetTiposClienteAsync(CancellationToken cancellationToken);
    Task<TipoClienteDto> CreateTipoClienteAsync(CreateTipoClienteRequestDto request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ClienteDto>> GetClientesAsync(string? search, CancellationToken cancellationToken);
    Task<ClienteDto> CreateClienteAsync(UpsertClienteRequestDto request, CancellationToken cancellationToken);
    Task<ClienteDto> UpdateClienteAsync(long clienteEmpresaId, UpsertClienteRequestDto request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ProductoDto>> GetProductosAsync(CancellationToken cancellationToken);
    Task<ProductoDto> CreateProductoAsync(UpsertProductoRequestDto request, CancellationToken cancellationToken);
    Task<ProductoDto> UpdateProductoAsync(long productoEmpresaId, UpsertProductoRequestDto request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<TarifaProductoResumenDto>> GetTarifasByProductoAsync(long productoEmpresaId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<TarifaDto>> GetTarifasAsync(string? tipoClienteCodigo, CancellationToken cancellationToken);
    Task<TarifaDto> CreateTarifaAsync(UpsertTarifaRequestDto request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<TarifaDto>> CreateTarifasBatchAsync(CreateTarifasBatchRequestDto request, CancellationToken cancellationToken);
    Task<TarifaDto> UpdateTarifaAsync(long tarifaProductoId, UpsertTarifaRequestDto request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ClaseAgendaDto>> GetClasesAsync(bool? activo, CancellationToken cancellationToken);
    Task<ClaseDto> GetClaseByIdAsync(long claseId, CancellationToken cancellationToken);
    Task<ClaseDto> CreateClaseAsync(UpsertClaseRequestDto request, CancellationToken cancellationToken);
    Task<ClaseDto> UpdateClaseAsync(long claseId, UpsertClaseRequestDto request, CancellationToken cancellationToken);
}
