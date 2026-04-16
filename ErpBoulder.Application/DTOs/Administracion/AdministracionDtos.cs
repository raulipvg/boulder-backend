namespace ErpBoulder.Application.DTOs.Administracion;

public sealed record LookupDto(long Id, string Codigo, string Nombre);

public sealed record BloqueHorarioDto(long BloqueHorarioComercialId, string Nombre, TimeOnly HoraInicio, TimeOnly HoraFin, bool Activo);

public sealed record UpsertBloqueHorarioRequestDto(string Nombre, TimeOnly HoraInicio, TimeOnly HoraFin, bool Activo);

public sealed record EmpresaDto(long EmpresaId, string NombreComercial, string? RazonSocial, string Rut, string Estado, string MonedaCodigo, string? TelefonoContacto, string? CorreoContacto);

public sealed record CreateEmpresaRequestDto(string NombreComercial, string? RazonSocial, string Rut, string? TelefonoContacto, string? CorreoContacto);

public sealed record UsuarioDto(long UsuarioId, string NombreCompleto, string Rut, string EmailLogin, string Estado, IReadOnlyCollection<string> Roles, long? EmpresaId, string? EmpresaNombre);

public sealed record CreateUsuarioRequestDto(string NombreCompleto, string Rut, string EmailLogin, string Password, string RolCodigo, long? EmpresaId);

public sealed record UpdateUsuarioRequestDto(string NombreCompleto, string Rut, string EmailLogin, string Estado, string RolCodigo, long? EmpresaId);

public sealed record ChangeUsuarioPasswordRequestDto(string NuevaPassword);

public sealed record TipoClienteDto(long TipoClienteId, string Codigo, string Nombre, bool Activo);

public sealed record CreateTipoClienteRequestDto(string Codigo, string Nombre);

public sealed record ClienteDto(long ClienteEmpresaId, long PersonaId, string NombreCompleto, string Rut, DateOnly? FechaNacimiento, string? Correo, string? Telefono, long TipoClienteId, string TipoCliente, string Estado);

public sealed record UpsertClienteRequestDto(string NombreCompleto, string Rut, DateOnly? FechaNacimiento, string? Telefono, string? Correo, long TipoClienteId, string Estado);

public sealed record ProductoDto(long ProductoEmpresaId, string NombreComercial, string? Descripcion, string TipoProductoBaseCodigo, string ModoPrecio, decimal? PrecioFijo, bool VisiblePos, bool Activo, bool RequiereCliente, bool GeneraBeneficio, long? BloqueHorarioComercialId, long? ClaseId, int? VigenciaDias, int? UsosIncluidos, bool AccesoIlimitado);

public sealed record UpsertProductoRequestDto(long TipoProductoBaseId, string NombreComercial, string? Descripcion, string ModoPrecio, decimal? PrecioFijo, bool VisiblePos, bool Activo, bool RequiereCliente, bool GeneraBeneficio, long? BloqueHorarioComercialId, long? ClaseId, int? VigenciaDias, int? UsosIncluidos, bool AccesoIlimitado);

public sealed record TarifaDto(long TarifaProductoId, long ProductoEmpresaId, string ProductoNombre, long? TipoClienteId, string? TipoClienteNombre, string? TipoDia, long? BloqueHorarioComercialId, decimal Precio, DateOnly VigenciaDesde, DateOnly VigenciaHasta, bool Activo);

public sealed record UpsertTarifaRequestDto(long ProductoEmpresaId, long? TipoClienteId, string? TipoDia, long? BloqueHorarioComercialId, decimal Precio, DateOnly VigenciaDesde, DateOnly VigenciaHasta, bool Activo);

public sealed record ClaseHorarioDto(long ClaseHorarioId, short DiaSemana, TimeOnly HoraInicio, TimeOnly HoraFin, bool Activo);

public sealed record ClaseDto(long ClaseId, string Nombre, long ProfesorEmpresaId, string ProfesorNombre, int CupoMaximo, string Estado, IReadOnlyCollection<ClaseHorarioDto> Horarios);

public sealed record ClaseHorarioRequestDto(short DiaSemana, TimeOnly HoraInicio, TimeOnly HoraFin, bool Activo);

public sealed record UpsertClaseRequestDto(string Nombre, long ProfesorEmpresaId, int CupoMaximo, string Estado, IReadOnlyCollection<ClaseHorarioRequestDto> Horarios);
