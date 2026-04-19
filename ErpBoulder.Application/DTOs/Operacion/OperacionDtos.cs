namespace ErpBoulder.Application.DTOs.Operacion;

public sealed record ClienteLookupDto(long ClienteEmpresaId, string NombreCompleto, string Rut, string Estado, string TipoCliente);

public sealed record AccessOptionDto(
    long BeneficioClienteId,
    string ProductoNombre,
    string Estado,
    DateOnly FechaInicio,
    DateOnly FechaTermino,
    int? UsosTotales,
    int UsosConsumidos,
    bool PuedeValidarAhora,
    bool YaValidadoHoy,
    bool DentroBloqueHorario,
    string? MotivoNoValidable);

public sealed record AccessPreviewDto(long ClienteEmpresaId, string ClienteNombre, string EstadoCliente, IReadOnlyCollection<AccessOptionDto> Opciones);

public sealed record ValidateAccessRequestDto(long ClienteEmpresaId, long BeneficioClienteId);

public sealed record AccessValidationResultDto(bool Autorizado, string Mensaje, long? AccesoEventoId, long? BeneficioClienteId, string? ProductoNombre);

public sealed record ClaseSesionDto(long ClaseSesionId, DateOnly Fecha, TimeOnly HoraInicio, TimeOnly HoraFin, string ClaseNombre, string ProfesorNombre, int CupoMaximo, string Estado);

public sealed record ClaseSesionInscritoDto(
    long ClienteEmpresaId,
    string ClienteNombre,
    string Rut,
    string EstadoCliente,
    long BeneficioClienteId,
    string ProductoNombre,
    int? UsosTotales,
    int UsosConsumidos,
    bool AccesoIlimitado,
    bool AsistenciaRegistrada);

public sealed record RegisterAttendanceRequestDto(long ClaseSesionId, long ClienteEmpresaId, long BeneficioClienteId);

public sealed record ClaseAsistenciaDto(long ClaseAsistenciaId, long ClaseSesionId, long ClienteEmpresaId, string Estado, DateTimeOffset FechaHoraRegistro);
