namespace ErpBoulder.Infrastructure.Services;

using BCrypt.Net;
using ErpBoulder.Application.DTOs.Administracion;
using ErpBoulder.Application.Interfaces.Common;
using ErpBoulder.Application.Interfaces.Services;
using ErpBoulder.Domain.Constants;
using ErpBoulder.Domain.Entities.Administracion;
using ErpBoulder.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public sealed class AdministracionService : ServiceBase, IAdministracionService
{
    public AdministracionService(ErpBoulderDbContext dbContext, ICurrentUserContext currentUser)
        : base(dbContext, currentUser)
    {
    }

    public async Task<IReadOnlyCollection<EmpresaDto>> GetEmpresasAsync(CancellationToken cancellationToken)
    {
        var query = DbContext.Empresas.AsNoTracking();

        if (!CurrentUser.IsInRole(RoleCodes.AdminTotal))
        {
            var empresaId = GetRequiredEmpresaId();
            query = query.Where(x => x.EmpresaId == empresaId);
        }

        return await query
            .OrderBy(x => x.NombreComercial)
            .Select(x => new EmpresaDto(x.EmpresaId, x.NombreComercial, x.RazonSocial, x.Rut, x.Estado, x.MonedaCodigo, x.TelefonoContacto, x.CorreoContacto))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<LookupDto>> GetTiposProductoBaseAsync(CancellationToken cancellationToken)
    {
        return await DbContext.TiposProductoBase.AsNoTracking()
            .OrderBy(x => x.Nombre)
            .Select(x => new LookupDto(x.TipoProductoBaseId, x.Codigo, x.Nombre))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<LookupDto>> GetMediosPagoAsync(CancellationToken cancellationToken)
    {
        return await DbContext.MediosPago.AsNoTracking()
            .Where(x => x.Activo)
            .OrderBy(x => x.Nombre)
            .Select(x => new LookupDto(x.MedioPagoId, x.Codigo, x.Nombre))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<LookupDto>> GetBloquesHorariosAsync(CancellationToken cancellationToken)
    {
        var empresaId = GetRequiredEmpresaId();

        return await DbContext.BloquesHorariosComerciales.AsNoTracking()
            .Where(x => x.EmpresaId == empresaId && x.Activo)
            .OrderBy(x => x.HoraInicio)
            .Select(x => new LookupDto(
                x.BloqueHorarioComercialId,
                x.Nombre,
                $"{x.Nombre} ({x.HoraInicio:HH\\:mm}-{x.HoraFin:HH\\:mm})"))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<BloqueHorarioDto>> GetBloquesHorariosComercialesAsync(CancellationToken cancellationToken)
    {
        var empresaId = GetRequiredEmpresaId();

        return await DbContext.BloquesHorariosComerciales.AsNoTracking()
            .Where(x => x.EmpresaId == empresaId)
            .OrderBy(x => x.HoraInicio)
            .Select(x => new BloqueHorarioDto(x.BloqueHorarioComercialId, x.Nombre, x.HoraInicio, x.HoraFin, x.Activo))
            .ToListAsync(cancellationToken);
    }

    public async Task<BloqueHorarioDto> CreateBloqueHorarioAsync(UpsertBloqueHorarioRequestDto request, CancellationToken cancellationToken)
    {
        var empresaId = GetRequiredEmpresaId();

        if (request.HoraInicio >= request.HoraFin)
            throw new InvalidOperationException("La hora de inicio debe ser anterior a la hora de fin.");

        if (request.Activo)
            await ValidateBloqueHorarioOverlapAsync(empresaId, request.HoraInicio, request.HoraFin, null, cancellationToken);

        var entity = new BloqueHorarioComercial
        {
            EmpresaId = empresaId,
            Nombre = request.Nombre.Trim(),
            HoraInicio = request.HoraInicio,
            HoraFin = request.HoraFin,
            Activo = request.Activo
        };

        DbContext.BloquesHorariosComerciales.Add(entity);
        await DbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync("bloque_horario_comercial", entity.BloqueHorarioComercialId, "crear", request, empresaId, cancellationToken);

        return new BloqueHorarioDto(entity.BloqueHorarioComercialId, entity.Nombre, entity.HoraInicio, entity.HoraFin, entity.Activo);
    }

    public async Task<BloqueHorarioDto> UpdateBloqueHorarioAsync(long bloqueHorarioComercialId, UpsertBloqueHorarioRequestDto request, CancellationToken cancellationToken)
    {
        var empresaId = GetRequiredEmpresaId();

        if (request.HoraInicio >= request.HoraFin)
            throw new InvalidOperationException("La hora de inicio debe ser anterior a la hora de fin.");

        var entity = await DbContext.BloquesHorariosComerciales
            .FirstAsync(x => x.BloqueHorarioComercialId == bloqueHorarioComercialId && x.EmpresaId == empresaId, cancellationToken);

        if (request.Activo)
            await ValidateBloqueHorarioOverlapAsync(empresaId, request.HoraInicio, request.HoraFin, bloqueHorarioComercialId, cancellationToken);

        entity.Nombre = request.Nombre.Trim();
        entity.HoraInicio = request.HoraInicio;
        entity.HoraFin = request.HoraFin;
        entity.Activo = request.Activo;

        await DbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync("bloque_horario_comercial", entity.BloqueHorarioComercialId, "actualizar", request, empresaId, cancellationToken);

        return new BloqueHorarioDto(entity.BloqueHorarioComercialId, entity.Nombre, entity.HoraInicio, entity.HoraFin, entity.Activo);
    }

    private async Task ValidateBloqueHorarioOverlapAsync(long empresaId, TimeOnly horaInicio, TimeOnly horaFin, long? excludeId, CancellationToken cancellationToken)
    {
        var query = DbContext.BloquesHorariosComerciales.AsNoTracking()
            .Where(x => x.EmpresaId == empresaId && x.Activo && horaInicio < x.HoraFin && x.HoraInicio < horaFin);

        if (excludeId.HasValue)
            query = query.Where(x => x.BloqueHorarioComercialId != excludeId.Value);

        var overlap = await query
            .Select(x => new { x.Nombre, x.HoraInicio, x.HoraFin })
            .FirstOrDefaultAsync(cancellationToken);

        if (overlap is not null)
            throw new InvalidOperationException($"El horario se solapa con el bloque activo '{overlap.Nombre}' ({overlap.HoraInicio:HH\\:mm}-{overlap.HoraFin:HH\\:mm}).");
    }

    public async Task<IReadOnlyCollection<LookupDto>> GetProfesoresAsync(CancellationToken cancellationToken)
    {
        var empresaId = GetRequiredEmpresaId();

        return await DbContext.ProfesoresEmpresa.AsNoTracking()
            .Include(x => x.Persona)
            .Where(x => x.EmpresaId == empresaId && x.Estado == "activo")
            .OrderBy(x => x.Persona.NombreCompleto)
            .Select(x => new LookupDto(x.ProfesorEmpresaId, x.Persona.Rut, x.Persona.NombreCompleto))
            .ToListAsync(cancellationToken);
    }

    public async Task<EmpresaDto> CreateEmpresaAsync(CreateEmpresaRequestDto request, CancellationToken cancellationToken)
    {
        if (!CurrentUser.IsInRole(RoleCodes.AdminTotal))
        {
            throw new InvalidOperationException("Solo ADMIN_TOTAL puede crear empresas.");
        }

        var entity = new Empresa
        {
            NombreComercial = request.NombreComercial,
            RazonSocial = request.RazonSocial,
            Rut = request.Rut,
            Estado = "activa",
            MonedaCodigo = "CLP",
            TelefonoContacto = request.TelefonoContacto,
            CorreoContacto = request.CorreoContacto,
            Timezone = "America/Santiago",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        DbContext.Empresas.Add(entity);
        await DbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync("empresa", entity.EmpresaId, "crear", new { entity.NombreComercial, entity.Rut }, entity.EmpresaId, cancellationToken);

        return new EmpresaDto(entity.EmpresaId, entity.NombreComercial, entity.RazonSocial, entity.Rut, entity.Estado, entity.MonedaCodigo, entity.TelefonoContacto, entity.CorreoContacto);
    }

    public async Task<EmpresaDto> UpdateEmpresaAsync(long empresaId, CreateEmpresaRequestDto request, CancellationToken cancellationToken)
    {
        if (!CurrentUser.IsInRole(RoleCodes.AdminTotal))
        {
            throw new InvalidOperationException("Solo ADMIN_TOTAL puede editar empresas.");
        }

        var entity = await DbContext.Empresas.FirstAsync(x => x.EmpresaId == empresaId, cancellationToken);
        entity.NombreComercial = request.NombreComercial;
        entity.RazonSocial = request.RazonSocial;
        entity.Rut = request.Rut;
        entity.TelefonoContacto = request.TelefonoContacto;
        entity.CorreoContacto = request.CorreoContacto;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync("empresa", entity.EmpresaId, "actualizar", new { entity.NombreComercial, entity.Rut }, entity.EmpresaId, cancellationToken);

        return new EmpresaDto(entity.EmpresaId, entity.NombreComercial, entity.RazonSocial, entity.Rut, entity.Estado, entity.MonedaCodigo, entity.TelefonoContacto, entity.CorreoContacto);
    }

    public async Task<IReadOnlyCollection<UsuarioDto>> GetUsuariosAsync(CancellationToken cancellationToken)
    {
        var empresaId = GetOptionalEmpresaId();

        var query = DbContext.UsuarioRoles
            .AsNoTracking()
            .Include(x => x.Usuario).ThenInclude(x => x.Persona)
            .Include(x => x.Rol)
            .Include(x => x.Empresa)
            .Where(x => x.Activo);

        if (empresaId.HasValue)
        {
            query = query.Where(x => x.EmpresaId == empresaId || (CurrentUser.IsInRole(RoleCodes.AdminTotal) && x.EmpresaId == null));
        }
        else if (!CurrentUser.IsInRole(RoleCodes.AdminTotal))
        {
            throw new InvalidOperationException("Debe existir contexto de empresa.");
        }

        var rows = await query.ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.UsuarioId)
            .Select(group => new UsuarioDto(
                group.Key,
                group.First().Usuario.Persona.NombreCompleto,
                group.First().Usuario.Persona.Rut,
                group.First().Usuario.EmailLogin,
                group.First().Usuario.Estado,
                group.Select(x => x.Rol.Codigo).Distinct().ToArray(),
                group.Select(x => x.EmpresaId).FirstOrDefault(x => x.HasValue),
                group.Select(x => x.Empresa?.NombreComercial).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))))
            .OrderBy(x => x.NombreCompleto)
            .ToList();
    }

    public async Task<UsuarioDto> CreateUsuarioAsync(CreateUsuarioRequestDto request, CancellationToken cancellationToken)
    {
        var targetEmpresaId = request.EmpresaId;

        if (CurrentUser.IsInRole(RoleCodes.AdminEmpresa))
        {
            targetEmpresaId = GetRequiredEmpresaId();

            if (request.RolCodigo == RoleCodes.AdminTotal)
            {
                throw new InvalidOperationException("No puede crear usuarios globales.");
            }
        }

        if (!CurrentUser.IsInRole(RoleCodes.AdminTotal) && !CurrentUser.IsInRole(RoleCodes.AdminEmpresa))
        {
            throw new InvalidOperationException("No tiene permisos para crear usuarios.");
        }

        var persona = new Persona
        {
            NombreCompleto = request.NombreCompleto,
            Rut = request.Rut,
            Correo = request.EmailLogin,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var usuario = new Usuario
        {
            Persona = persona,
            EmailLogin = request.EmailLogin,
            PasswordHash = BCrypt.HashPassword(request.Password),
            Estado = "activo",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var rol = await DbContext.Roles.FirstAsync(x => x.Codigo == request.RolCodigo, cancellationToken);

        usuario.Roles.Add(new UsuarioRol
        {
            RolId = rol.RolId,
            EmpresaId = targetEmpresaId,
            Activo = true,
            CreatedAt = DateTimeOffset.UtcNow
        });

        DbContext.Usuarios.Add(usuario);
        await DbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync("usuario", usuario.UsuarioId, "crear", new { usuario.EmailLogin, request.RolCodigo, targetEmpresaId }, targetEmpresaId, cancellationToken);

        return new UsuarioDto(usuario.UsuarioId, request.NombreCompleto, request.Rut, usuario.EmailLogin, usuario.Estado, new[] { request.RolCodigo }, targetEmpresaId, targetEmpresaId.HasValue ? await DbContext.Empresas.Where(x => x.EmpresaId == targetEmpresaId).Select(x => x.NombreComercial).FirstAsync(cancellationToken) : null);
    }

    public async Task<UsuarioDto> UpdateUsuarioAsync(long usuarioId, UpdateUsuarioRequestDto request, CancellationToken cancellationToken)
    {
        var targetEmpresaId = request.EmpresaId;

        if (CurrentUser.IsInRole(RoleCodes.AdminEmpresa))
        {
            targetEmpresaId = GetRequiredEmpresaId();

            if (request.RolCodigo == RoleCodes.AdminTotal)
            {
                throw new InvalidOperationException("No puede asignar rol ADMIN_TOTAL.");
            }
        }

        if (!CurrentUser.IsInRole(RoleCodes.AdminTotal) && !CurrentUser.IsInRole(RoleCodes.AdminEmpresa))
        {
            throw new InvalidOperationException("No tiene permisos para editar usuarios.");
        }

        var roleRowQuery = DbContext.UsuarioRoles
            .Include(x => x.Usuario)
            .ThenInclude(x => x.Persona)
            .Include(x => x.Rol)
            .Where(x => x.UsuarioId == usuarioId && x.Activo);

        if (CurrentUser.IsInRole(RoleCodes.AdminEmpresa))
        {
            var empresaId = GetRequiredEmpresaId();
            roleRowQuery = roleRowQuery.Where(x => x.EmpresaId == empresaId);
        }

        var roleRow = await roleRowQuery.OrderBy(x => x.UsuarioRolId).FirstAsync(cancellationToken);
        var usuario = roleRow.Usuario;
        var persona = usuario.Persona;
        var rol = await DbContext.Roles.FirstAsync(x => x.Codigo == request.RolCodigo, cancellationToken);

        persona.NombreCompleto = request.NombreCompleto;
        persona.Rut = request.Rut;
        persona.Correo = request.EmailLogin;
        persona.UpdatedAt = DateTimeOffset.UtcNow;

        usuario.EmailLogin = request.EmailLogin;
        usuario.Estado = request.Estado;

        roleRow.RolId = rol.RolId;
        roleRow.EmpresaId = targetEmpresaId;

        await DbContext.SaveChangesAsync(cancellationToken);

        var empresaNombre = roleRow.EmpresaId.HasValue
            ? await DbContext.Empresas.Where(x => x.EmpresaId == roleRow.EmpresaId).Select(x => x.NombreComercial).FirstOrDefaultAsync(cancellationToken)
            : null;

        await AuditAsync("usuario", usuario.UsuarioId, "actualizar", new { usuario.EmailLogin, request.RolCodigo, roleRow.EmpresaId }, roleRow.EmpresaId, cancellationToken);

        return new UsuarioDto(usuario.UsuarioId, persona.NombreCompleto, persona.Rut, usuario.EmailLogin, usuario.Estado, new[] { request.RolCodigo }, roleRow.EmpresaId, empresaNombre);
    }

    public async Task ChangeUsuarioPasswordAsync(long usuarioId, ChangeUsuarioPasswordRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.NuevaPassword))
        {
            throw new InvalidOperationException("La nueva contraseña es obligatoria.");
        }

        if (!CurrentUser.IsInRole(RoleCodes.AdminTotal) && !CurrentUser.IsInRole(RoleCodes.AdminEmpresa))
        {
            throw new InvalidOperationException("No tiene permisos para cambiar contraseñas.");
        }

        var roleRowQuery = DbContext.UsuarioRoles
            .Include(x => x.Usuario)
            .Where(x => x.UsuarioId == usuarioId && x.Activo);

        if (CurrentUser.IsInRole(RoleCodes.AdminEmpresa))
        {
            var empresaId = GetRequiredEmpresaId();
            roleRowQuery = roleRowQuery.Where(x => x.EmpresaId == empresaId);
        }

        var roleRow = await roleRowQuery.OrderBy(x => x.UsuarioRolId).FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Usuario no encontrado para el contexto actual.");

        roleRow.Usuario.PasswordHash = BCrypt.HashPassword(request.NuevaPassword);

        await DbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync("usuario", usuarioId, "actualizar_password", new { usuarioId }, roleRow.EmpresaId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<TipoClienteDto>> GetTiposClienteAsync(CancellationToken cancellationToken)
    {
        var empresaId = GetRequiredEmpresaId();

        return await DbContext.TiposCliente.AsNoTracking()
            .Where(x => x.EmpresaId == empresaId)
            .OrderBy(x => x.Nombre)
            .Select(x => new TipoClienteDto(x.TipoClienteId, x.Codigo, x.Nombre, x.Activo))
            .ToListAsync(cancellationToken);
    }

    public async Task<TipoClienteDto> CreateTipoClienteAsync(CreateTipoClienteRequestDto request, CancellationToken cancellationToken)
    {
        var empresaId = GetRequiredEmpresaId();

        var entity = new TipoCliente
        {
            EmpresaId = empresaId,
            Codigo = request.Codigo,
            Nombre = request.Nombre,
            Activo = true
        };

        DbContext.TiposCliente.Add(entity);
        await DbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync("tipo_cliente", entity.TipoClienteId, "crear", request, empresaId, cancellationToken);

        return new TipoClienteDto(entity.TipoClienteId, entity.Codigo, entity.Nombre, entity.Activo);
    }

    public async Task<IReadOnlyCollection<ClienteDto>> GetClientesAsync(string? search, CancellationToken cancellationToken)
    {
        var empresaId = GetRequiredEmpresaId();

        var query = DbContext.ClientesEmpresa
            .AsNoTracking()
            .Include(x => x.Persona)
            .Include(x => x.TipoCliente)
            .Where(x => x.EmpresaId == empresaId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = $"%{search.Trim()}%";
            query = query.Where(x => EF.Functions.ILike(x.Persona.NombreCompleto, searchTerm) || EF.Functions.ILike(x.Persona.Rut, searchTerm));
        }

        return await query
            .OrderBy(x => x.Persona.NombreCompleto)
            .Select(x => new ClienteDto(
                x.ClienteEmpresaId,
                x.PersonaId,
                x.Persona.NombreCompleto,
                x.Persona.Rut,
                x.Persona.FechaNacimiento,
                x.Persona.Correo,
                x.Persona.Telefono,
                x.TipoClienteId,
                x.TipoCliente.Nombre,
                x.Estado))
            .ToListAsync(cancellationToken);
    }

    public async Task<ClienteDto> CreateClienteAsync(UpsertClienteRequestDto request, CancellationToken cancellationToken)
    {
        var empresaId = GetRequiredEmpresaId();
        var nombreNormalizado = NormalizeName(request.NombreCompleto);
        if (string.IsNullOrWhiteSpace(nombreNormalizado))
        {
            throw new InvalidOperationException("El nombre del cliente es obligatorio.");
        }

        var rutNormalizado = NormalizeRut(request.Rut);
        if (!IsValidRut(rutNormalizado))
        {
            throw new InvalidOperationException("El RUT ingresado no es válido.");
        }

        var tipoClienteValido = await DbContext.TiposCliente
            .AsNoTracking()
            .AnyAsync(x => x.EmpresaId == empresaId && x.TipoClienteId == request.TipoClienteId && x.Activo, cancellationToken);

        if (!tipoClienteValido)
        {
            throw new InvalidOperationException("Tipo de cliente inválido para la empresa actual.");
        }

        var estado = string.IsNullOrWhiteSpace(request.Estado) ? "activo" : request.Estado;
        var persona = await DbContext.Personas.FirstOrDefaultAsync(x => x.Rut == rutNormalizado, cancellationToken);

        if (persona is null)
        {
            persona = new Persona
            {
                Rut = rutNormalizado,
                NombreCompleto = nombreNormalizado,
                FechaNacimiento = request.FechaNacimiento,
                Telefono = request.Telefono,
                Correo = request.Correo,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            DbContext.Personas.Add(persona);
            await DbContext.SaveChangesAsync(cancellationToken);
        }

        var existing = await DbContext.ClientesEmpresa.FirstOrDefaultAsync(x => x.EmpresaId == empresaId && x.PersonaId == persona.PersonaId, cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException("El cliente ya existe en esta empresa.");
        }

        var entity = new ClienteEmpresa
        {
            EmpresaId = empresaId,
            PersonaId = persona.PersonaId,
            TipoClienteId = request.TipoClienteId,
            Estado = estado,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        DbContext.ClientesEmpresa.Add(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        var tipoClienteNombre = await DbContext.TiposCliente.Where(x => x.TipoClienteId == entity.TipoClienteId).Select(x => x.Nombre).FirstAsync(cancellationToken);
        await AuditAsync("cliente_empresa", entity.ClienteEmpresaId, "crear", new { persona.Rut, entity.TipoClienteId }, empresaId, cancellationToken);

        return new ClienteDto(entity.ClienteEmpresaId, persona.PersonaId, persona.NombreCompleto, persona.Rut, persona.FechaNacimiento, persona.Correo, persona.Telefono, entity.TipoClienteId, tipoClienteNombre, entity.Estado);
    }

    public async Task<ClienteDto> UpdateClienteAsync(long clienteEmpresaId, UpsertClienteRequestDto request, CancellationToken cancellationToken)
    {
        var empresaId = GetRequiredEmpresaId();
        var nombreNormalizado = NormalizeName(request.NombreCompleto);
        if (string.IsNullOrWhiteSpace(nombreNormalizado))
        {
            throw new InvalidOperationException("El nombre del cliente es obligatorio.");
        }

        var rutNormalizado = NormalizeRut(request.Rut);
        if (!IsValidRut(rutNormalizado))
        {
            throw new InvalidOperationException("El RUT ingresado no es válido.");
        }

        var tipoClienteValido = await DbContext.TiposCliente
            .AsNoTracking()
            .AnyAsync(x => x.EmpresaId == empresaId && x.TipoClienteId == request.TipoClienteId, cancellationToken);

        if (!tipoClienteValido)
        {
            throw new InvalidOperationException("Tipo de cliente inválido para la empresa actual.");
        }

        var estado = string.IsNullOrWhiteSpace(request.Estado) ? "activo" : request.Estado;

        var entity = await DbContext.ClientesEmpresa
            .Include(x => x.Persona)
            .Include(x => x.TipoCliente)
            .FirstAsync(x => x.ClienteEmpresaId == clienteEmpresaId && x.EmpresaId == empresaId, cancellationToken);

        var persona = entity.Persona;
        persona.NombreCompleto = nombreNormalizado;
        persona.Rut = rutNormalizado;
        persona.FechaNacimiento = request.FechaNacimiento;
        persona.Telefono = request.Telefono;
        persona.Correo = request.Correo;
        persona.UpdatedAt = DateTimeOffset.UtcNow;

        entity.TipoClienteId = request.TipoClienteId;
        entity.Estado = estado;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        var tipoClienteNombre = await DbContext.TiposCliente.Where(x => x.TipoClienteId == entity.TipoClienteId).Select(x => x.Nombre).FirstAsync(cancellationToken);
        await AuditAsync("cliente_empresa", entity.ClienteEmpresaId, "actualizar", new { persona.Rut, entity.TipoClienteId, entity.Estado }, empresaId, cancellationToken);

        return new ClienteDto(entity.ClienteEmpresaId, persona.PersonaId, persona.NombreCompleto, persona.Rut, persona.FechaNacimiento, persona.Correo, persona.Telefono, entity.TipoClienteId, tipoClienteNombre, entity.Estado);
    }

    public async Task<IReadOnlyCollection<ProductoDto>> GetProductosAsync(CancellationToken cancellationToken)
    {
        var empresaId = GetRequiredEmpresaId();

        return await DbContext.ProductosEmpresa
            .AsNoTracking()
            .Include(x => x.TipoProductoBase)
            .Where(x => x.EmpresaId == empresaId)
            .OrderBy(x => x.NombreComercial)
            .Select(x => new ProductoDto(
                x.ProductoEmpresaId,
                x.NombreComercial,
                x.Descripcion,
                x.TipoProductoBase.Codigo,
                x.ModoPrecio,
                x.PrecioFijo,
                x.VisiblePos,
                x.Activo,
                x.TarifaAsociada,
                x.RequiereCliente,
                x.GeneraBeneficio,
                x.BloqueHorarioComercialId,
                x.ClaseId,
                x.VigenciaDias,
                x.UsosIncluidos,
                x.AccesoIlimitado))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<TarifaProductoResumenDto>> GetTarifasByProductoAsync(long productoEmpresaId, CancellationToken cancellationToken)
    {
        var empresaId = GetRequiredEmpresaId();

        var productoExiste = await DbContext.ProductosEmpresa
            .AsNoTracking()
            .AnyAsync(x => x.ProductoEmpresaId == productoEmpresaId && x.EmpresaId == empresaId, cancellationToken);

        if (!productoExiste)
        {
            return Array.Empty<TarifaProductoResumenDto>();
        }

        var tarifas = await DbContext.TarifasProducto
            .AsNoTracking()
            .Where(x => x.ProductoEmpresaId == productoEmpresaId)
            .ToListAsync(cancellationToken);

        if (!tarifas.Any())
        {
            return Array.Empty<TarifaProductoResumenDto>();
        }

        var tipoClienteIds = tarifas.Select(t => t.TipoClienteId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var bloqueIds = tarifas.Select(t => t.BloqueHorarioComercialId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();

        var tiposCliente = await DbContext.TiposCliente
            .AsNoTracking()
            .Where(t => t.EmpresaId == empresaId && tipoClienteIds.Contains(t.TipoClienteId))
            .ToDictionaryAsync(t => t.TipoClienteId, cancellationToken);

        var bloques = await DbContext.BloquesHorariosComerciales
            .AsNoTracking()
            .Where(b => b.EmpresaId == empresaId && bloqueIds.Contains(b.BloqueHorarioComercialId))
            .ToDictionaryAsync(b => b.BloqueHorarioComercialId, cancellationToken);

        return tarifas
            .Select(tarifa => new TarifaProductoResumenDto(
                tarifa.TarifaProductoId,
                tarifa.ProductoEmpresaId,
                tarifa.TipoClienteId,
                tarifa.TipoClienteId.HasValue && tiposCliente.TryGetValue(tarifa.TipoClienteId.Value, out var tc) ? tc.Nombre : null,
                tarifa.TipoDia,
                tarifa.BloqueHorarioComercialId,
                tarifa.BloqueHorarioComercialId.HasValue && bloques.TryGetValue(tarifa.BloqueHorarioComercialId.Value, out var bl) ? bl.Nombre : null,
                tarifa.BloqueHorarioComercialId.HasValue && bloques.TryGetValue(tarifa.BloqueHorarioComercialId.Value, out var bl2) ? bl2.HoraInicio.ToString(@"hh\:mm") : null,
                tarifa.BloqueHorarioComercialId.HasValue && bloques.TryGetValue(tarifa.BloqueHorarioComercialId.Value, out var bl3) ? bl3.HoraFin.ToString(@"hh\:mm") : null,
                tarifa.Precio,
                tarifa.VigenciaDesde,
                tarifa.VigenciaHasta,
                tarifa.Activo))
            .OrderByDescending(x => x.Activo)
            .ThenByDescending(x => x.VigenciaDesde)
            .ThenBy(x => x.TipoClienteNombre)
            .ToList();
    }

    public async Task<ProductoDto> CreateProductoAsync(UpsertProductoRequestDto request, CancellationToken cancellationToken)
    {
        var empresaId = GetRequiredEmpresaId();
        var tipoProductoBase = await DbContext.TiposProductoBase
            .AsNoTracking()
            .FirstAsync(x => x.TipoProductoBaseId == request.TipoProductoBaseId, cancellationToken);

        var normalizedRequest = NormalizeProductoRequestByType(request, tipoProductoBase.Codigo);
        var tarifaAsociada = false;

        if (normalizedRequest.ModoPrecio == "tarifa")
        {
            if (normalizedRequest.Activo || normalizedRequest.VisiblePos)
            {
                throw new InvalidOperationException("No se puede activar ni mostrar en POS un producto sin tarifa activa asociada.");
            }

            normalizedRequest = normalizedRequest with
            {
                Activo = false,
                VisiblePos = false,
            };
        }

        if (tipoProductoBase.Codigo == ProductBaseCodes.MensualidadPorHorario)
        {
            if (!normalizedRequest.BloqueHorarioComercialId.HasValue)
            {
                throw new InvalidOperationException("La mensualidad por horario requiere un bloque horario comercial.");
            }

            await EnsureActiveBloqueHorarioAsync(empresaId, normalizedRequest.BloqueHorarioComercialId.Value, cancellationToken);

            if (normalizedRequest.Activo)
            {
                await ValidateMensualidadPorHorarioOverlapAsync(empresaId, normalizedRequest.BloqueHorarioComercialId.Value, null, cancellationToken);
            }
        }

        if (ProductBaseCodes.IsPackTickets(tipoProductoBase.Codigo))
        {
            ValidatePackTicketsConfiguration(normalizedRequest);
        }

        if (tipoProductoBase.Codigo == ProductBaseCodes.Clases)
        {
            ValidateClasesConfiguration(normalizedRequest);
        }

        if (tipoProductoBase.Codigo == ProductBaseCodes.TicketIndividual)
        {
            ValidateTicketIndividualConfiguration(normalizedRequest);
        }

        var entity = new ProductoEmpresa
        {
            EmpresaId = empresaId,
            TipoProductoBaseId = normalizedRequest.TipoProductoBaseId,
            NombreComercial = normalizedRequest.NombreComercial,
            Descripcion = normalizedRequest.Descripcion,
            ModoPrecio = normalizedRequest.ModoPrecio,
            PrecioFijo = normalizedRequest.PrecioFijo,
            VisiblePos = normalizedRequest.VisiblePos,
            Activo = normalizedRequest.Activo,
            TarifaAsociada = tarifaAsociada,
            RequiereCliente = normalizedRequest.RequiereCliente,
            GeneraBeneficio = normalizedRequest.GeneraBeneficio,
            BloqueHorarioComercialId = normalizedRequest.BloqueHorarioComercialId,
            ClaseId = normalizedRequest.ClaseId,
            VigenciaDias = normalizedRequest.VigenciaDias,
            UsosIncluidos = normalizedRequest.UsosIncluidos,
            AccesoIlimitado = normalizedRequest.AccesoIlimitado,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        DbContext.ProductosEmpresa.Add(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        await AuditAsync("producto_empresa", entity.ProductoEmpresaId, "crear", normalizedRequest, empresaId, cancellationToken);

        return new ProductoDto(entity.ProductoEmpresaId, entity.NombreComercial, entity.Descripcion, tipoProductoBase.Codigo, entity.ModoPrecio, entity.PrecioFijo, entity.VisiblePos, entity.Activo, entity.TarifaAsociada, entity.RequiereCliente, entity.GeneraBeneficio, entity.BloqueHorarioComercialId, entity.ClaseId, entity.VigenciaDias, entity.UsosIncluidos, entity.AccesoIlimitado);
    }

    public async Task<ProductoDto> UpdateProductoAsync(long productoEmpresaId, UpsertProductoRequestDto request, CancellationToken cancellationToken)
    {
        var empresaId = GetRequiredEmpresaId();
        var entity = await DbContext.ProductosEmpresa
            .FirstAsync(x => x.ProductoEmpresaId == productoEmpresaId && x.EmpresaId == empresaId, cancellationToken);

        var hasActiveTarifaAsociada = await DbContext.TarifasProducto
            .AsNoTracking()
            .AnyAsync(x => x.ProductoEmpresaId == productoEmpresaId && x.Activo, cancellationToken);

        var tipoProductoBase = await DbContext.TiposProductoBase
            .AsNoTracking()
            .FirstAsync(x => x.TipoProductoBaseId == request.TipoProductoBaseId, cancellationToken);

        var normalizedRequest = NormalizeProductoRequestByType(request, tipoProductoBase.Codigo);
        var tarifaAsociada = normalizedRequest.ModoPrecio == "tarifa" && hasActiveTarifaAsociada;

        if (!normalizedRequest.Activo)
        {
            normalizedRequest = normalizedRequest with
            {
                VisiblePos = false,
            };
        }

        if (normalizedRequest.ModoPrecio == "tarifa" && !tarifaAsociada)
        {
            if (normalizedRequest.Activo || normalizedRequest.VisiblePos)
            {
                throw new InvalidOperationException("No se puede activar ni mostrar en POS un producto sin tarifa activa asociada.");
            }

            normalizedRequest = normalizedRequest with
            {
                Activo = false,
                VisiblePos = false,
            };
        }

        if (tipoProductoBase.Codigo == ProductBaseCodes.MensualidadPorHorario)
        {
            if (!normalizedRequest.BloqueHorarioComercialId.HasValue)
            {
                throw new InvalidOperationException("La mensualidad por horario requiere un bloque horario comercial.");
            }

            await EnsureActiveBloqueHorarioAsync(empresaId, normalizedRequest.BloqueHorarioComercialId.Value, cancellationToken);

            if (normalizedRequest.Activo)
            {
                await ValidateMensualidadPorHorarioOverlapAsync(empresaId, normalizedRequest.BloqueHorarioComercialId.Value, productoEmpresaId, cancellationToken);
            }
        }

        if (ProductBaseCodes.IsPackTickets(tipoProductoBase.Codigo))
        {
            ValidatePackTicketsConfiguration(normalizedRequest);
        }

        if (tipoProductoBase.Codigo == ProductBaseCodes.Clases)
        {
            ValidateClasesConfiguration(normalizedRequest);
        }

        if (tipoProductoBase.Codigo == ProductBaseCodes.TicketIndividual)
        {
            ValidateTicketIndividualConfiguration(normalizedRequest);
        }

        entity.TipoProductoBaseId = normalizedRequest.TipoProductoBaseId;
        entity.NombreComercial = normalizedRequest.NombreComercial;
        entity.Descripcion = normalizedRequest.Descripcion;
        entity.ModoPrecio = normalizedRequest.ModoPrecio;
        entity.PrecioFijo = normalizedRequest.PrecioFijo;
        entity.VisiblePos = normalizedRequest.VisiblePos;
        entity.Activo = normalizedRequest.Activo;
        entity.TarifaAsociada = tarifaAsociada;
        entity.RequiereCliente = normalizedRequest.RequiereCliente;
        entity.GeneraBeneficio = normalizedRequest.GeneraBeneficio;
        entity.BloqueHorarioComercialId = normalizedRequest.BloqueHorarioComercialId;
        entity.ClaseId = normalizedRequest.ClaseId;
        entity.VigenciaDias = normalizedRequest.VigenciaDias;
        entity.UsosIncluidos = normalizedRequest.UsosIncluidos;
        entity.AccesoIlimitado = normalizedRequest.AccesoIlimitado;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        await AuditAsync("producto_empresa", entity.ProductoEmpresaId, "actualizar", normalizedRequest, empresaId, cancellationToken);

        return new ProductoDto(entity.ProductoEmpresaId, entity.NombreComercial, entity.Descripcion, tipoProductoBase.Codigo, entity.ModoPrecio, entity.PrecioFijo, entity.VisiblePos, entity.Activo, entity.TarifaAsociada, entity.RequiereCliente, entity.GeneraBeneficio, entity.BloqueHorarioComercialId, entity.ClaseId, entity.VigenciaDias, entity.UsosIncluidos, entity.AccesoIlimitado);
    }

    public async Task<IReadOnlyCollection<TarifaDto>> GetTarifasAsync(string? tipoClienteCodigo, CancellationToken cancellationToken)
    {
        var empresaId = GetRequiredEmpresaId();
        var normalizedTipoClienteCodigo = string.IsNullOrWhiteSpace(tipoClienteCodigo)
            ? "GENERAL"
            : tipoClienteCodigo.Trim().ToUpperInvariant();

        var tipoCliente = await DbContext.TiposCliente
            .AsNoTracking()
            .Where(x => x.EmpresaId == empresaId && x.Codigo == normalizedTipoClienteCodigo)
            .Select(x => new { x.TipoClienteId })
            .FirstOrDefaultAsync(cancellationToken);

        if (tipoCliente is null)
        {
            return Array.Empty<TarifaDto>();
        }

        var tarifas = await DbContext.TarifasProducto
            .AsNoTracking()
            .Join(DbContext.ProductosEmpresa.Where(p => p.EmpresaId == empresaId), x => x.ProductoEmpresaId, y => y.ProductoEmpresaId, (tarifa, producto) => new { tarifa, producto })
            .Where(x => x.tarifa.TipoClienteId == tipoCliente.TipoClienteId)
            .ToListAsync(cancellationToken);

        var tipoClienteIds = tarifas.Select(x => x.tarifa.TipoClienteId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var tiposCliente = await DbContext.TiposCliente.Where(tc => tipoClienteIds.Contains(tc.TipoClienteId)).ToDictionaryAsync(tc => tc.TipoClienteId, cancellationToken);

        return tarifas.OrderBy(x => x.producto.NombreComercial).Select(x => new TarifaDto(
            x.tarifa.TarifaProductoId,
            x.tarifa.ProductoEmpresaId,
            x.producto.NombreComercial,
            x.tarifa.TipoClienteId,
            x.tarifa.TipoClienteId.HasValue && tiposCliente.TryGetValue(x.tarifa.TipoClienteId.Value, out var tc) ? tc.Nombre : null,
            x.tarifa.TipoDia,
            x.tarifa.BloqueHorarioComercialId,
            x.tarifa.Precio,
            x.tarifa.VigenciaDesde,
            x.tarifa.VigenciaHasta,
            x.tarifa.Activo)).ToList();
    }

    public async Task<TarifaDto> CreateTarifaAsync(UpsertTarifaRequestDto request, CancellationToken cancellationToken)
    {
        var empresaId = GetRequiredEmpresaId();
        var tipoDiaNormalizado = NormalizeTipoDiaCsv(request.TipoDia);

        if (!request.TipoClienteId.HasValue)
        {
            throw new InvalidOperationException("Tipo de cliente es obligatorio.");
        }

        var producto = await DbContext.ProductosEmpresa.FirstAsync(x => x.ProductoEmpresaId == request.ProductoEmpresaId && x.EmpresaId == empresaId, cancellationToken);
        if (producto.ModoPrecio != "tarifa")
        {
            throw new InvalidOperationException("Solo se pueden asociar tarifas a productos con modo de precio 'tarifa'.");
        }

        if (request.Activo)
        {
            await ValidateActiveTarifaOverlapAsync(
                request.ProductoEmpresaId,
                request.TipoClienteId,
                request.BloqueHorarioComercialId,
                tipoDiaNormalizado,
                request.VigenciaDesde,
                request.VigenciaHasta,
                null,
                cancellationToken);
        }

        var entity = new TarifaProducto
        {
            ProductoEmpresaId = request.ProductoEmpresaId,
            TipoClienteId = request.TipoClienteId,
            TipoDia = tipoDiaNormalizado,
            BloqueHorarioComercialId = request.BloqueHorarioComercialId,
            Precio = request.Precio,
            VigenciaDesde = request.VigenciaDesde,
            VigenciaHasta = request.VigenciaHasta,
            Activo = request.Activo,
            CreatedAt = DateTimeOffset.UtcNow
        };

        DbContext.TarifasProducto.Add(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        var productoActualizado = await RefreshProductoTarifaAsociadaAsync(empresaId, entity.ProductoEmpresaId, cancellationToken);
        if (productoActualizado)
        {
            await DbContext.SaveChangesAsync(cancellationToken);
        }

        await AuditAsync("tarifa_producto", entity.TarifaProductoId, "crear", request, empresaId, cancellationToken);

        var tipoClienteNombre = entity.TipoClienteId.HasValue
            ? await DbContext.TiposCliente.Where(x => x.TipoClienteId == entity.TipoClienteId).Select(x => x.Nombre).FirstOrDefaultAsync(cancellationToken)
            : null;

        return new TarifaDto(entity.TarifaProductoId, entity.ProductoEmpresaId, producto.NombreComercial, entity.TipoClienteId, tipoClienteNombre, entity.TipoDia, entity.BloqueHorarioComercialId, entity.Precio, entity.VigenciaDesde, entity.VigenciaHasta, entity.Activo);
    }

    public async Task<IReadOnlyCollection<TarifaDto>> CreateTarifasBatchAsync(CreateTarifasBatchRequestDto request, CancellationToken cancellationToken)
    {
        var empresaId = GetRequiredEmpresaId();

        if (request.Tarifas is null || request.Tarifas.Count == 0)
        {
            throw new InvalidOperationException("Debes agregar al menos una tarifa.");
        }

        var producto = await DbContext.ProductosEmpresa
            .FirstAsync(x => x.ProductoEmpresaId == request.ProductoEmpresaId && x.EmpresaId == empresaId, cancellationToken);

        if (producto.ModoPrecio != "tarifa")
        {
            throw new InvalidOperationException("Solo se pueden asociar tarifas a productos con modo de precio 'tarifa'.");
        }

        var batchActiveEntries = new List<(int Linea, long TipoClienteId, long? BloqueHorarioComercialId, string TipoDiaCsv, DateOnly VigenciaDesde, DateOnly VigenciaHasta)>();
        var entities = new List<TarifaProducto>();

        await using var tx = await DbContext.Database.BeginTransactionAsync(cancellationToken);

        for (var index = 0; index < request.Tarifas.Count; index++)
        {
            var linea = request.Tarifas.ElementAt(index);
            var lineNumber = index + 1;

            if (linea.TipoClienteId <= 0)
            {
                throw new InvalidOperationException($"Línea {lineNumber}: Tipo de cliente es obligatorio.");
            }

            if (linea.Precio <= 0)
            {
                throw new InvalidOperationException($"Línea {lineNumber}: El precio debe ser mayor a 0.");
            }

            if (linea.VigenciaHasta < linea.VigenciaDesde)
            {
                throw new InvalidOperationException($"Línea {lineNumber}: La vigencia hasta no puede ser anterior a la vigencia desde.");
            }

            if (linea.BloqueHorarioComercialId.HasValue)
            {
                await EnsureActiveBloqueHorarioAsync(empresaId, linea.BloqueHorarioComercialId.Value, cancellationToken);
            }

            var tipoDiaNormalizado = NormalizeTipoDiaCsv(linea.TipoDia);

            if (linea.Activo)
            {
                await ValidateActiveTarifaOverlapAsync(
                    request.ProductoEmpresaId,
                    linea.TipoClienteId,
                    linea.BloqueHorarioComercialId,
                    tipoDiaNormalizado,
                    linea.VigenciaDesde,
                    linea.VigenciaHasta,
                    null,
                    cancellationToken);

                var hasOverlapInBatch = batchActiveEntries.Any(existing =>
                    existing.TipoClienteId == linea.TipoClienteId
                    && existing.BloqueHorarioComercialId == linea.BloqueHorarioComercialId
                    && existing.VigenciaDesde <= linea.VigenciaHasta
                    && linea.VigenciaDesde <= existing.VigenciaHasta
                    && HasTipoDiaIntersection(existing.TipoDiaCsv, tipoDiaNormalizado));

                if (hasOverlapInBatch)
                {
                    throw new InvalidOperationException($"Línea {lineNumber}: Ya existe otra tarifa activa solapada en el lote para la misma combinación de tipo de cliente, bloque, días y vigencia.");
                }

                batchActiveEntries.Add((lineNumber, linea.TipoClienteId, linea.BloqueHorarioComercialId, tipoDiaNormalizado, linea.VigenciaDesde, linea.VigenciaHasta));
            }

            var entity = new TarifaProducto
            {
                ProductoEmpresaId = request.ProductoEmpresaId,
                TipoClienteId = linea.TipoClienteId,
                TipoDia = tipoDiaNormalizado,
                BloqueHorarioComercialId = linea.BloqueHorarioComercialId,
                Precio = linea.Precio,
                VigenciaDesde = linea.VigenciaDesde,
                VigenciaHasta = linea.VigenciaHasta,
                Activo = linea.Activo,
                CreatedAt = DateTimeOffset.UtcNow
            };

            entities.Add(entity);
            DbContext.TarifasProducto.Add(entity);
        }

        await DbContext.SaveChangesAsync(cancellationToken);

        var productoActualizado = await RefreshProductoTarifaAsociadaAsync(empresaId, request.ProductoEmpresaId, cancellationToken);
        if (productoActualizado)
        {
            await DbContext.SaveChangesAsync(cancellationToken);
        }

        foreach (var entity in entities)
        {
            await AuditAsync("tarifa_producto", entity.TarifaProductoId, "crear", request, empresaId, cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);

        var tipoClienteIds = entities.Select(x => x.TipoClienteId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var tiposCliente = await DbContext.TiposCliente
            .AsNoTracking()
            .Where(x => tipoClienteIds.Contains(x.TipoClienteId))
            .ToDictionaryAsync(x => x.TipoClienteId, cancellationToken);

        return entities.Select(entity => new TarifaDto(
            entity.TarifaProductoId,
            entity.ProductoEmpresaId,
            producto.NombreComercial,
            entity.TipoClienteId,
            entity.TipoClienteId.HasValue && tiposCliente.TryGetValue(entity.TipoClienteId.Value, out var tipoCliente) ? tipoCliente.Nombre : null,
            entity.TipoDia,
            entity.BloqueHorarioComercialId,
            entity.Precio,
            entity.VigenciaDesde,
            entity.VigenciaHasta,
            entity.Activo)).ToList();
    }

    public async Task<TarifaDto> UpdateTarifaAsync(long tarifaProductoId, UpsertTarifaRequestDto request, CancellationToken cancellationToken)
    {
        var empresaId = GetRequiredEmpresaId();
        var tipoDiaNormalizado = NormalizeTipoDiaCsv(request.TipoDia);

        if (!request.TipoClienteId.HasValue)
        {
            throw new InvalidOperationException("Tipo de cliente es obligatorio.");
        }

        var producto = await DbContext.ProductosEmpresa.FirstAsync(x => x.ProductoEmpresaId == request.ProductoEmpresaId && x.EmpresaId == empresaId, cancellationToken);
        if (producto.ModoPrecio != "tarifa")
        {
            throw new InvalidOperationException("Solo se pueden asociar tarifas a productos con modo de precio 'tarifa'.");
        }

        var entity = await DbContext.TarifasProducto
            .Join(DbContext.ProductosEmpresa.Where(p => p.EmpresaId == empresaId), t => t.ProductoEmpresaId, p => p.ProductoEmpresaId, (t, p) => t)
            .FirstAsync(x => x.TarifaProductoId == tarifaProductoId, cancellationToken);
        var previousProductoEmpresaId = entity.ProductoEmpresaId;

        if (request.Activo)
        {
            await ValidateActiveTarifaOverlapAsync(
                request.ProductoEmpresaId,
                request.TipoClienteId,
                request.BloqueHorarioComercialId,
                tipoDiaNormalizado,
                request.VigenciaDesde,
                request.VigenciaHasta,
                tarifaProductoId,
                cancellationToken);
        }

        entity.ProductoEmpresaId = request.ProductoEmpresaId;
        entity.TipoClienteId = request.TipoClienteId;
        entity.TipoDia = tipoDiaNormalizado;
        entity.BloqueHorarioComercialId = request.BloqueHorarioComercialId;
        entity.Precio = request.Precio;
        entity.VigenciaDesde = request.VigenciaDesde;
        entity.VigenciaHasta = request.VigenciaHasta;
        entity.Activo = request.Activo;

        await DbContext.SaveChangesAsync(cancellationToken);

        var oldProductoActualizado = await RefreshProductoTarifaAsociadaAsync(empresaId, previousProductoEmpresaId, cancellationToken);
        var newProductoActualizado = previousProductoEmpresaId == entity.ProductoEmpresaId
            ? false
            : await RefreshProductoTarifaAsociadaAsync(empresaId, entity.ProductoEmpresaId, cancellationToken);

        if (oldProductoActualizado || newProductoActualizado)
        {
            await DbContext.SaveChangesAsync(cancellationToken);
        }

        await AuditAsync("tarifa_producto", entity.TarifaProductoId, "actualizar", request, empresaId, cancellationToken);

        var tipoClienteNombre = entity.TipoClienteId.HasValue
            ? await DbContext.TiposCliente.Where(x => x.TipoClienteId == entity.TipoClienteId).Select(x => x.Nombre).FirstOrDefaultAsync(cancellationToken)
            : null;

        return new TarifaDto(entity.TarifaProductoId, entity.ProductoEmpresaId, producto.NombreComercial, entity.TipoClienteId, tipoClienteNombre, entity.TipoDia, entity.BloqueHorarioComercialId, entity.Precio, entity.VigenciaDesde, entity.VigenciaHasta, entity.Activo);
    }

    public async Task<IReadOnlyCollection<ClaseDto>> GetClasesAsync(bool? activo, CancellationToken cancellationToken)
    {
        var empresaId = GetRequiredEmpresaId();
        var horarioActivoFiltro = activo;

        var query = DbContext.Clases
            .AsNoTracking()
            .Include(x => x.ProfesorEmpresa).ThenInclude(x => x.Persona)
            .Include(x => x.Horarios)
            .Where(x => x.EmpresaId == empresaId);

        if (activo.HasValue)
        {
            var horarioActivoFiltroValue = activo.Value;
            query = query.Where(x => x.Activo == activo.Value && x.Horarios.Any(h => h.Activo == horarioActivoFiltroValue));
        }

        return await query
            .OrderBy(x => x.Nombre)
            .Select(x => new ClaseDto(
                x.ClaseId,
                x.Nombre,
                x.ProfesorEmpresaId,
                x.ProfesorEmpresa.Persona.NombreCompleto,
                x.CupoMaximo,
                x.Activo,
                x.Horarios
                    .Where(h => !horarioActivoFiltro.HasValue || h.Activo == horarioActivoFiltro.Value)
                    .OrderBy(h => h.DiaSemana)
                    .ThenBy(h => h.HoraInicio)
                    .Select(h => new ClaseHorarioDto(h.ClaseHorarioId, h.DiaSemana, h.HoraInicio, h.HoraFin, h.Activo))
                    .ToArray()))
            .ToListAsync(cancellationToken);
    }

    public async Task<ClaseDto> GetClaseByIdAsync(long claseId, CancellationToken cancellationToken)
    {
        var empresaId = GetRequiredEmpresaId();

        return await DbContext.Clases
            .AsNoTracking()
            .Include(x => x.ProfesorEmpresa).ThenInclude(x => x.Persona)
            .Include(x => x.Horarios)
            .Where(x => x.EmpresaId == empresaId && x.ClaseId == claseId)
            .Select(x => new ClaseDto(
                x.ClaseId,
                x.Nombre,
                x.ProfesorEmpresaId,
                x.ProfesorEmpresa.Persona.NombreCompleto,
                x.CupoMaximo,
                x.Activo,
                x.Horarios
                    .OrderBy(h => h.DiaSemana)
                    .ThenBy(h => h.HoraInicio)
                    .Select(h => new ClaseHorarioDto(h.ClaseHorarioId, h.DiaSemana, h.HoraInicio, h.HoraFin, h.Activo))
                    .ToArray()))
            .FirstAsync(cancellationToken);
    }

    public async Task<ClaseDto> CreateClaseAsync(UpsertClaseRequestDto request, CancellationToken cancellationToken)
    {
        var empresaId = GetRequiredEmpresaId();
        var normalizedRequest = request with
        {
            Horarios = (request.Horarios ?? Array.Empty<ClaseHorarioRequestDto>())
                .Select(h => h with { Activo = request.Activo })
                .ToArray()
        };

        var profesor = await DbContext.ProfesoresEmpresa
            .Include(x => x.Persona)
            .FirstAsync(x => x.ProfesorEmpresaId == normalizedRequest.ProfesorEmpresaId && x.EmpresaId == empresaId, cancellationToken);

        await ValidateClaseHorariosAsync(empresaId, normalizedRequest.ProfesorEmpresaId, normalizedRequest.Activo, normalizedRequest.Horarios, null, cancellationToken);

        var entity = new Clase
        {
            EmpresaId = empresaId,
            Nombre = normalizedRequest.Nombre,
            ProfesorEmpresaId = normalizedRequest.ProfesorEmpresaId,
            CupoMaximo = normalizedRequest.CupoMaximo,
            Activo = normalizedRequest.Activo,
            Horarios = normalizedRequest.Horarios.Select(h => new ClaseHorario
            {
                DiaSemana = h.DiaSemana,
                HoraInicio = h.HoraInicio,
                HoraFin = h.HoraFin,
                Activo = h.Activo
            }).ToList()
        };

        DbContext.Clases.Add(entity);
        await DbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync("clase", entity.ClaseId, "crear", normalizedRequest, empresaId, cancellationToken);

        return new ClaseDto(entity.ClaseId, entity.Nombre, entity.ProfesorEmpresaId, profesor.Persona.NombreCompleto, entity.CupoMaximo, entity.Activo, entity.Horarios.Select(h => new ClaseHorarioDto(h.ClaseHorarioId, h.DiaSemana, h.HoraInicio, h.HoraFin, h.Activo)).ToArray());
    }

    public async Task<ClaseDto> UpdateClaseAsync(long claseId, UpsertClaseRequestDto request, CancellationToken cancellationToken)
    {
        var empresaId = GetRequiredEmpresaId();
        var normalizedRequest = request with
        {
            Horarios = (request.Horarios ?? Array.Empty<ClaseHorarioRequestDto>())
                .Select(h => h with { Activo = request.Activo })
                .ToArray()
        };

        var profesor = await DbContext.ProfesoresEmpresa
            .Include(x => x.Persona)
            .FirstAsync(x => x.ProfesorEmpresaId == normalizedRequest.ProfesorEmpresaId && x.EmpresaId == empresaId, cancellationToken);

        await ValidateClaseHorariosAsync(empresaId, normalizedRequest.ProfesorEmpresaId, normalizedRequest.Activo, normalizedRequest.Horarios, claseId, cancellationToken);

        var entity = await DbContext.Clases
            .Include(x => x.Horarios)
            .FirstAsync(x => x.ClaseId == claseId && x.EmpresaId == empresaId, cancellationToken);

        entity.Nombre = normalizedRequest.Nombre;
        entity.ProfesorEmpresaId = normalizedRequest.ProfesorEmpresaId;
        entity.CupoMaximo = normalizedRequest.CupoMaximo;
        entity.Activo = normalizedRequest.Activo;

        DbContext.ClaseHorarios.RemoveRange(entity.Horarios);
        entity.Horarios = normalizedRequest.Horarios.Select(h => new ClaseHorario
        {
            DiaSemana = h.DiaSemana,
            HoraInicio = h.HoraInicio,
            HoraFin = h.HoraFin,
            Activo = h.Activo
        }).ToList();

        await DbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync("clase", entity.ClaseId, "actualizar", normalizedRequest, empresaId, cancellationToken);

        return new ClaseDto(entity.ClaseId, entity.Nombre, entity.ProfesorEmpresaId, profesor.Persona.NombreCompleto, entity.CupoMaximo, entity.Activo, entity.Horarios.Select(h => new ClaseHorarioDto(h.ClaseHorarioId, h.DiaSemana, h.HoraInicio, h.HoraFin, h.Activo)).ToArray());
    }

    private async Task ValidateClaseHorariosAsync(
        long empresaId,
        long profesorEmpresaId,
        bool claseActiva,
        IReadOnlyCollection<ClaseHorarioRequestDto> horariosRequest,
        long? excludeClaseId,
        CancellationToken cancellationToken)
    {
        if (horariosRequest is null || horariosRequest.Count == 0)
        {
            throw new InvalidOperationException("Debe registrar al menos un horario para la clase.");
        }

        var activeHorarios = horariosRequest
            .Where(h => h.Activo)
            .OrderBy(h => h.DiaSemana)
            .ThenBy(h => h.HoraInicio)
            .ToList();

        foreach (var horario in horariosRequest)
        {
            if (horario.DiaSemana < 1 || horario.DiaSemana > 7)
            {
                throw new InvalidOperationException("El dia de semana debe estar entre 1 (lunes) y 7 (domingo).");
            }

            if (horario.HoraFin <= horario.HoraInicio)
            {
                throw new InvalidOperationException("La hora de fin debe ser mayor que la hora de inicio.");
            }
        }

        var activeHorariosByDay = activeHorarios
            .GroupBy(h => h.DiaSemana)
            .ToDictionary(group => group.Key, group => group.OrderBy(h => h.HoraInicio).ToList());

        foreach (var dayGroup in activeHorariosByDay.Values)
        {
            for (var index = 1; index < dayGroup.Count; index++)
            {
                var previous = dayGroup[index - 1];
                var current = dayGroup[index];
                if (current.HoraInicio < previous.HoraFin)
                {
                    throw new InvalidOperationException(
                        $"La clase tiene horarios cruzados el {GetDiaSemanaLabel(current.DiaSemana)} entre {current.HoraInicio:HH\\:mm} y {previous.HoraFin:HH\\:mm}.");
                }
            }
        }

        if (!claseActiva || activeHorarios.Count == 0)
        {
            return;
        }

        var existingActiveHorarios = await DbContext.Clases
            .AsNoTracking()
            .Where(x => x.EmpresaId == empresaId
                && x.ProfesorEmpresaId == profesorEmpresaId
                && x.Activo
                && (!excludeClaseId.HasValue || x.ClaseId != excludeClaseId.Value))
            .SelectMany(x => x.Horarios
                .Where(h => h.Activo)
                .Select(h => new { ClaseNombre = x.Nombre, h.DiaSemana, h.HoraInicio, h.HoraFin }))
            .ToListAsync(cancellationToken);

        foreach (var existing in existingActiveHorarios)
        {
            if (!activeHorariosByDay.TryGetValue(existing.DiaSemana, out var dayHorarios))
            {
                continue;
            }

            foreach (var requested in dayHorarios)
            {
                if (requested.HoraInicio < existing.HoraFin && existing.HoraInicio < requested.HoraFin)
                {
                    throw new InvalidOperationException(
                        $"El profesor ya tiene la clase '{existing.ClaseNombre}' el {GetDiaSemanaLabel(existing.DiaSemana)} entre {existing.HoraInicio:HH\\:mm} y {existing.HoraFin:HH\\:mm}.");
                }
            }
        }
    }

    private static string GetDiaSemanaLabel(short diaSemana)
        => diaSemana switch
        {
            1 => "lunes",
            2 => "martes",
            3 => "miercoles",
            4 => "jueves",
            5 => "viernes",
            6 => "sabado",
            7 => "domingo",
            _ => $"dia {diaSemana}"
        };

    private static UpsertProductoRequestDto NormalizeProductoRequestByType(UpsertProductoRequestDto request, string tipoCodigo)
    {
        if (tipoCodigo is ProductBaseCodes.MensualidadPorHorario or ProductBaseCodes.MensualidadTodoHorario)
        {
            return request with
            {
                ModoPrecio = "tarifa",
                PrecioFijo = null,
                RequiereCliente = true,
                GeneraBeneficio = true,
                BloqueHorarioComercialId = tipoCodigo == ProductBaseCodes.MensualidadPorHorario ? request.BloqueHorarioComercialId : null,
                ClaseId = null,
                VigenciaDias = 30,
                UsosIncluidos = null,
                AccesoIlimitado = true,
            };
        }

        if (ProductBaseCodes.IsPackTickets(tipoCodigo))
        {
            return request with
            {
                ModoPrecio = "tarifa",
                PrecioFijo = null,
                RequiereCliente = true,
                GeneraBeneficio = true,
                ClaseId = null,
                AccesoIlimitado = false,
                // BloqueHorarioComercialId se preserva: puede ser null (libre) o tener valor
            };
        }

        if (tipoCodigo == ProductBaseCodes.Clases)
        {
            return request with
            {
                ModoPrecio = "tarifa",
                PrecioFijo = null,
                RequiereCliente = true,
                GeneraBeneficio = true,
                AccesoIlimitado = false,
                VigenciaDias = 30,
                BloqueHorarioComercialId = null,
            };
        }

        if (tipoCodigo == ProductBaseCodes.ProductoCaja)
        {
            return request with
            {
                ModoPrecio = "fijo",
                VigenciaDias = null,
                UsosIncluidos = null,
                BloqueHorarioComercialId = null,
                ClaseId = null,
                RequiereCliente = false,
                GeneraBeneficio = false,
                AccesoIlimitado = false,
            };
        }

        if (tipoCodigo == ProductBaseCodes.TicketIndividual)
        {
            return request with
            {
                ModoPrecio = "tarifa",
                PrecioFijo = null,
                VigenciaDias = null,
                UsosIncluidos = 1,
                ClaseId = null,
                RequiereCliente = true,
                GeneraBeneficio = false,
                AccesoIlimitado = false,
            };
        }

        return request;
    }

    private async Task ValidateActiveTarifaOverlapAsync(
        long productoEmpresaId,
        long? tipoClienteId,
        long? bloqueHorarioComercialId,
        string tipoDiaCsv,
        DateOnly vigenciaDesde,
        DateOnly vigenciaHasta,
        long? excludeTarifaProductoId,
        CancellationToken cancellationToken)
    {
        if (vigenciaHasta < vigenciaDesde)
        {
            throw new InvalidOperationException("La vigencia hasta no puede ser anterior a la vigencia desde.");
        }

        var query = DbContext.TarifasProducto
            .AsNoTracking()
            .Where(x =>
                x.Activo &&
                x.ProductoEmpresaId == productoEmpresaId &&
                x.TipoClienteId == tipoClienteId &&
                x.BloqueHorarioComercialId == bloqueHorarioComercialId &&
                x.VigenciaDesde <= vigenciaHasta &&
                vigenciaDesde <= x.VigenciaHasta);

        if (excludeTarifaProductoId.HasValue)
        {
            query = query.Where(x => x.TarifaProductoId != excludeTarifaProductoId.Value);
        }

        var candidates = await query
            .Select(x => new { x.TarifaProductoId, x.TipoDia, x.VigenciaDesde, x.VigenciaHasta })
            .ToListAsync(cancellationToken);

        var hasOverlap = candidates.Any(candidate =>
        {
            var existingTipoDia = string.IsNullOrWhiteSpace(candidate.TipoDia)
                ? string.Join(',', TipoDiaOrder)
                : NormalizeTipoDiaCsv(candidate.TipoDia);

            return HasTipoDiaIntersection(existingTipoDia, tipoDiaCsv);
        });

        if (hasOverlap)
        {
            throw new InvalidOperationException("Ya existe una tarifa activa solapada para esa combinación de producto, tipo de cliente, bloque horario y día.");
        }
    }

    private async Task<bool> RefreshProductoTarifaAsociadaAsync(long empresaId, long productoEmpresaId, CancellationToken cancellationToken)
    {
        var producto = await DbContext.ProductosEmpresa
            .FirstOrDefaultAsync(x => x.ProductoEmpresaId == productoEmpresaId && x.EmpresaId == empresaId, cancellationToken);

        if (producto is null)
        {
            return false;
        }

        var hasActiveTarifa = await DbContext.TarifasProducto
            .AsNoTracking()
            .AnyAsync(x => x.ProductoEmpresaId == productoEmpresaId && x.Activo, cancellationToken);

        var tarifaAsociada = producto.ModoPrecio == "tarifa" && hasActiveTarifa;
        var shouldDisableForMissingTarifa = producto.ModoPrecio == "tarifa" && !tarifaAsociada;

        var hasChanges = false;

        if (producto.TarifaAsociada != tarifaAsociada)
        {
            producto.TarifaAsociada = tarifaAsociada;
            hasChanges = true;
        }

        if (shouldDisableForMissingTarifa && producto.Activo)
        {
            producto.Activo = false;
            hasChanges = true;
        }

        if (shouldDisableForMissingTarifa && producto.VisiblePos)
        {
            producto.VisiblePos = false;
            hasChanges = true;
        }

        if (hasChanges)
        {
            producto.UpdatedAt = DateTimeOffset.UtcNow;
        }

        return hasChanges;
    }

    private static void ValidateClasesConfiguration(UpsertProductoRequestDto request)
    {
        if (!request.ClaseId.HasValue)
        {
            throw new InvalidOperationException("El producto de tipo Clases debe tener una clase asociada.");
        }
    }

    private static void ValidateTicketIndividualConfiguration(UpsertProductoRequestDto request)
    {
        if (!request.BloqueHorarioComercialId.HasValue)
        {
            throw new InvalidOperationException("El ticket individual debe estar asociado a un bloque horario.");
        }
    }

    private static void ValidatePackTicketsConfiguration(UpsertProductoRequestDto request)
    {
        if (!request.UsosIncluidos.HasValue || request.UsosIncluidos.Value <= 0)
        {
            throw new InvalidOperationException("El pack de tickets debe definir una cantidad de tickets mayor a 0.");
        }

        if (!request.VigenciaDias.HasValue || request.VigenciaDias.Value <= 0)
        {
            throw new InvalidOperationException("El pack de tickets debe definir vigencia en días mayor a 0.");
        }
    }

    private async Task EnsureActiveBloqueHorarioAsync(long empresaId, long bloqueHorarioComercialId, CancellationToken cancellationToken)
    {
        var exists = await DbContext.BloquesHorariosComerciales
            .AsNoTracking()
            .AnyAsync(x => x.EmpresaId == empresaId && x.BloqueHorarioComercialId == bloqueHorarioComercialId && x.Activo, cancellationToken);

        if (!exists)
        {
            throw new InvalidOperationException("El bloque horario seleccionado no existe o no está activo para la empresa.");
        }
    }

    private async Task ValidateMensualidadPorHorarioOverlapAsync(long empresaId, long bloqueHorarioComercialId, long? excludeProductoEmpresaId, CancellationToken cancellationToken)
    {
        var selectedBloque = await DbContext.BloquesHorariosComerciales
            .AsNoTracking()
            .Where(x => x.EmpresaId == empresaId && x.BloqueHorarioComercialId == bloqueHorarioComercialId)
            .Select(x => new { x.BloqueHorarioComercialId, x.Nombre, x.HoraInicio, x.HoraFin })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("El bloque horario seleccionado no existe para la empresa.");

        var overlapping = await DbContext.ProductosEmpresa
            .AsNoTracking()
            .Join(
                DbContext.TiposProductoBase.AsNoTracking().Where(x => x.Codigo == ProductBaseCodes.MensualidadPorHorario),
                producto => producto.TipoProductoBaseId,
                tipo => tipo.TipoProductoBaseId,
                (producto, _) => producto)
            .Where(x => x.EmpresaId == empresaId && x.Activo && x.BloqueHorarioComercialId.HasValue && (!excludeProductoEmpresaId.HasValue || x.ProductoEmpresaId != excludeProductoEmpresaId.Value))
            .Join(
                DbContext.BloquesHorariosComerciales.AsNoTracking(),
                producto => producto.BloqueHorarioComercialId!.Value,
                bloque => bloque.BloqueHorarioComercialId,
                (producto, bloque) => new { producto, bloque })
            .Where(x => selectedBloque.HoraInicio < x.bloque.HoraFin && x.bloque.HoraInicio < selectedBloque.HoraFin)
            .Select(x => new { x.producto.NombreComercial, x.bloque.Nombre, x.bloque.HoraInicio, x.bloque.HoraFin })
            .FirstOrDefaultAsync(cancellationToken);

        if (overlapping is not null)
        {
            throw new InvalidOperationException($"El bloque horario seleccionado se solapa con la mensualidad activa '{overlapping.NombreComercial}' ({overlapping.Nombre} {overlapping.HoraInicio:HH\\:mm}-{overlapping.HoraFin:HH\\:mm}).");
        }
    }

    private static string NormalizeName(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private static string NormalizeRut(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var alphanumeric = new string(value.Where(c => char.IsDigit(c) || c is 'k' or 'K').ToArray()).ToUpperInvariant();
        if (alphanumeric.Length < 2)
        {
            return string.Empty;
        }

        var body = alphanumeric[..^1];
        var dv = alphanumeric[^1];

        return $"{body}-{dv}";
    }

    private static bool IsValidRut(string rut)
    {
        if (string.IsNullOrWhiteSpace(rut))
        {
            return false;
        }

        var parts = rut.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !parts[0].All(char.IsDigit))
        {
            return false;
        }

        var body = parts[0];
        var dv = char.ToUpperInvariant(parts[1][0]);
        var sum = 0;
        var multiplier = 2;

        for (var i = body.Length - 1; i >= 0; i--)
        {
            sum += (body[i] - '0') * multiplier;
            multiplier = multiplier == 7 ? 2 : multiplier + 1;
        }

        var remainder = 11 - (sum % 11);
        var expected = remainder switch
        {
            11 => '0',
            10 => 'K',
            _ => remainder.ToString()[0]
        };

        return dv == expected;
    }
}
