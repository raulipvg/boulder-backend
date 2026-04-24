namespace ErpBoulder.Domain.Entities.Administracion;

public sealed class Empresa
{
    public long EmpresaId { get; set; }
    public string NombreComercial { get; set; } = string.Empty;
    public string? RazonSocial { get; set; }
    public string Rut { get; set; } = string.Empty;
    public string Estado { get; set; } = "activo";
    public string MonedaCodigo { get; set; } = "CLP";
    public string? TelefonoContacto { get; set; }
    public string? CorreoContacto { get; set; }
    public string Timezone { get; set; } = "America/Santiago";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class Persona
{
    public long PersonaId { get; set; }
    public string Rut { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public DateOnly? FechaNacimiento { get; set; }
    public string? Telefono { get; set; }
    public string? Correo { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class Usuario
{
    public long UsuarioId { get; set; }
    public long PersonaId { get; set; }
    public string EmailLogin { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Estado { get; set; } = "activo";
    public DateTimeOffset? UltimoAccesoAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Persona Persona { get; set; } = null!;
    public ICollection<UsuarioRol> Roles { get; set; } = new List<UsuarioRol>();
}

public sealed class Rol
{
    public long RolId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
}

public sealed class UsuarioRol
{
    public long UsuarioRolId { get; set; }
    public long UsuarioId { get; set; }
    public long RolId { get; set; }
    public long? EmpresaId { get; set; }
    public bool Activo { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Usuario Usuario { get; set; } = null!;
    public Rol Rol { get; set; } = null!;
    public Empresa? Empresa { get; set; }
}

public sealed class TipoCliente
{
    public long TipoClienteId { get; set; }
    public long EmpresaId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; }
}

public sealed class ClienteEmpresa
{
    public long ClienteEmpresaId { get; set; }
    public long EmpresaId { get; set; }
    public long PersonaId { get; set; }
    public long TipoClienteId { get; set; }
    public string Estado { get; set; } = "activo";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Empresa Empresa { get; set; } = null!;
    public Persona Persona { get; set; } = null!;
    public TipoCliente TipoCliente { get; set; } = null!;
}

public sealed class ProfesorEmpresa
{
    public long ProfesorEmpresaId { get; set; }
    public long EmpresaId { get; set; }
    public long PersonaId { get; set; }
    public string? Especialidad { get; set; }
    public string Estado { get; set; } = "activo";

    public Persona Persona { get; set; } = null!;
}

public sealed class Clase
{
    public long ClaseId { get; set; }
    public long EmpresaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public long ProfesorEmpresaId { get; set; }
    public int CupoMaximo { get; set; }
    public bool Activo { get; set; } = true;

    public ProfesorEmpresa ProfesorEmpresa { get; set; } = null!;
    public ICollection<ClaseHorario> Horarios { get; set; } = new List<ClaseHorario>();
}

public sealed class ClaseHorario
{
    public long ClaseHorarioId { get; set; }
    public long ClaseId { get; set; }
    public short DiaSemana { get; set; }
    public TimeOnly HoraInicio { get; set; }
    public TimeOnly HoraFin { get; set; }
    public bool Activo { get; set; }
}

public sealed class BloqueHorarioComercial
{
    public long BloqueHorarioComercialId { get; set; }
    public long EmpresaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public TimeOnly HoraInicio { get; set; }
    public TimeOnly HoraFin { get; set; }
    public bool Activo { get; set; }
}

public sealed class Feriado
{
    public DateOnly Fecha { get; set; }
    public string Nombre { get; set; } = string.Empty;
}

public sealed class TipoProductoBase
{
    public long TipoProductoBaseId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
}

public sealed class ProductoEmpresa
{
    public long ProductoEmpresaId { get; set; }
    public long EmpresaId { get; set; }
    public long TipoProductoBaseId { get; set; }
    public string NombreComercial { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string ModoPrecio { get; set; } = "fijo";
    public decimal? PrecioFijo { get; set; }
    public bool VisiblePos { get; set; }
    public bool Activo { get; set; }
    public bool TarifaAsociada { get; set; }
    public bool RequiereCliente { get; set; }
    public bool GeneraBeneficio { get; set; }
    public long? BloqueHorarioComercialId { get; set; }
    public long? ClaseId { get; set; }
    public int? VigenciaDias { get; set; }
    public int? UsosIncluidos { get; set; }
    public bool AccesoIlimitado { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public TipoProductoBase TipoProductoBase { get; set; } = null!;
    public BloqueHorarioComercial? BloqueHorarioComercial { get; set; }
    public Clase? Clase { get; set; }
}

public sealed class TarifaProducto
{
    public long TarifaProductoId { get; set; }
    public long ProductoEmpresaId { get; set; }
    public long? TipoClienteId { get; set; }
    public string? TipoDia { get; set; }
    public long? BloqueHorarioComercialId { get; set; }
    public decimal Precio { get; set; }
    public DateOnly VigenciaDesde { get; set; }
    public DateOnly VigenciaHasta { get; set; }
    public bool Activo { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class MedioPago
{
    public long MedioPagoId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; }
}
