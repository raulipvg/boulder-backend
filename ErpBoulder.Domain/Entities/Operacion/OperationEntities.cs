namespace ErpBoulder.Domain.Entities.Operacion;

public sealed class ClaseSesion
{
    public long ClaseSesionId { get; set; }
    public long EmpresaId { get; set; }
    public long ClaseId { get; set; }
    public DateOnly Fecha { get; set; }
    public TimeOnly HoraInicio { get; set; }
    public TimeOnly HoraFin { get; set; }
    public long ProfesorEmpresaId { get; set; }
    public int CupoMaximo { get; set; }
    public string Estado { get; set; } = "programada";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class AccesoEvento
{
    public long AccesoEventoId { get; set; }
    public long EmpresaId { get; set; }
    public long ClienteEmpresaId { get; set; }
    public long? BeneficioClienteId { get; set; }
    public long? ProductoEmpresaId { get; set; }
    public long UsuarioValidadorId { get; set; }
    public DateTimeOffset FechaHora { get; set; }
    public string Resultado { get; set; } = string.Empty;
    public string? MotivoRechazo { get; set; }
}

public sealed class ClaseAsistencia
{
    public long ClaseAsistenciaId { get; set; }
    public long ClaseSesionId { get; set; }
    public long ClienteEmpresaId { get; set; }
    public long BeneficioClienteId { get; set; }
    public long UsuarioRegistroId { get; set; }
    public DateTimeOffset FechaHoraRegistro { get; set; }
    public string Estado { get; set; } = "asistio";
}

public sealed class AuditoriaEvento
{
    public long AuditoriaEventoId { get; set; }
    public long? EmpresaId { get; set; }
    public long? UsuarioId { get; set; }
    public string Entidad { get; set; } = string.Empty;
    public long? EntidadId { get; set; }
    public string Accion { get; set; } = string.Empty;
    public DateTimeOffset FechaHora { get; set; }
    public string DetalleJson { get; set; } = "{}";
}
