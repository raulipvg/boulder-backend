namespace ErpBoulder.Application.Interfaces.Services;

using ErpBoulder.Application.DTOs.Operacion;

public interface IOperacionService
{
    Task<IReadOnlyCollection<ClienteLookupDto>> BuscarClientesAsync(string? search, CancellationToken cancellationToken);
    Task<AccessPreviewDto> PrevisualizarAccesoAsync(long clienteEmpresaId, CancellationToken cancellationToken);
    Task<AccessValidationResultDto> ValidarAccesoAsync(ValidateAccessRequestDto request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ClaseSesionDto>> GetSesionesAsync(DateOnly? fecha, CancellationToken cancellationToken);
    Task<ClaseAsistenciaDto> RegistrarAsistenciaAsync(RegisterAttendanceRequestDto request, CancellationToken cancellationToken);
}
