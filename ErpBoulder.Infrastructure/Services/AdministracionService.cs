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
            .Select(x => new EmpresaDto(x.EmpresaId, x.NombreComercial, x.Rut, x.Estado, x.MonedaCodigo, x.CorreoContacto))
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
            .OrderBy(x => x.Nombre)
            .Select(x => new LookupDto(x.BloqueHorarioComercialId, x.Nombre, x.Nombre))
            .ToListAsync(cancellationToken);
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

        return new EmpresaDto(entity.EmpresaId, entity.NombreComercial, entity.Rut, entity.Estado, entity.MonedaCodigo, entity.CorreoContacto);
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

        return new UsuarioDto(usuario.UsuarioId, request.NombreCompleto, usuario.EmailLogin, usuario.Estado, new[] { request.RolCodigo }, targetEmpresaId, targetEmpresaId.HasValue ? await DbContext.Empresas.Where(x => x.EmpresaId == targetEmpresaId).Select(x => x.NombreComercial).FirstAsync(cancellationToken) : null);
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
            query = query.Where(x => x.Persona.NombreCompleto.Contains(search) || x.Persona.Rut.Contains(search));
        }

        return await query
            .OrderBy(x => x.Persona.NombreCompleto)
            .Select(x => new ClienteDto(
                x.ClienteEmpresaId,
                x.PersonaId,
                x.Persona.NombreCompleto,
                x.Persona.Rut,
                x.Persona.Correo,
                x.Persona.Telefono,
                x.TipoCliente.Nombre,
                x.Estado))
            .ToListAsync(cancellationToken);
    }

    public async Task<ClienteDto> CreateClienteAsync(UpsertClienteRequestDto request, CancellationToken cancellationToken)
    {
        var empresaId = GetRequiredEmpresaId();
        var persona = await DbContext.Personas.FirstOrDefaultAsync(x => x.Rut == request.Rut, cancellationToken);

        if (persona is null)
        {
            persona = new Persona
            {
                Rut = request.Rut,
                NombreCompleto = request.NombreCompleto,
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
            Estado = request.Estado,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        DbContext.ClientesEmpresa.Add(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        var tipoClienteNombre = await DbContext.TiposCliente.Where(x => x.TipoClienteId == entity.TipoClienteId).Select(x => x.Nombre).FirstAsync(cancellationToken);
        await AuditAsync("cliente_empresa", entity.ClienteEmpresaId, "crear", new { persona.Rut, entity.TipoClienteId }, empresaId, cancellationToken);

        return new ClienteDto(entity.ClienteEmpresaId, persona.PersonaId, persona.NombreCompleto, persona.Rut, persona.Correo, persona.Telefono, tipoClienteNombre, entity.Estado);
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
                x.TipoProductoBase.Codigo,
                x.ModoPrecio,
                x.PrecioFijo,
                x.VisiblePos,
                x.Activo,
                x.RequiereCliente,
                x.GeneraBeneficio,
                x.BloqueHorarioComercialId,
                x.ClaseId,
                x.VigenciaDias,
                x.UsosIncluidos,
                x.AccesoIlimitado))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProductoDto> CreateProductoAsync(UpsertProductoRequestDto request, CancellationToken cancellationToken)
    {
        var empresaId = GetRequiredEmpresaId();

        var entity = new ProductoEmpresa
        {
            EmpresaId = empresaId,
            TipoProductoBaseId = request.TipoProductoBaseId,
            NombreComercial = request.NombreComercial,
            Descripcion = request.Descripcion,
            ModoPrecio = request.ModoPrecio,
            PrecioFijo = request.PrecioFijo,
            VisiblePos = request.VisiblePos,
            Activo = request.Activo,
            RequiereCliente = request.RequiereCliente,
            GeneraBeneficio = request.GeneraBeneficio,
            BloqueHorarioComercialId = request.BloqueHorarioComercialId,
            ClaseId = request.ClaseId,
            VigenciaDias = request.VigenciaDias,
            UsosIncluidos = request.UsosIncluidos,
            AccesoIlimitado = request.AccesoIlimitado,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        DbContext.ProductosEmpresa.Add(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        var tipoCodigo = await DbContext.TiposProductoBase.Where(x => x.TipoProductoBaseId == request.TipoProductoBaseId).Select(x => x.Codigo).FirstAsync(cancellationToken);
        await AuditAsync("producto_empresa", entity.ProductoEmpresaId, "crear", request, empresaId, cancellationToken);

        return new ProductoDto(entity.ProductoEmpresaId, entity.NombreComercial, tipoCodigo, entity.ModoPrecio, entity.PrecioFijo, entity.VisiblePos, entity.Activo, entity.RequiereCliente, entity.GeneraBeneficio, entity.BloqueHorarioComercialId, entity.ClaseId, entity.VigenciaDias, entity.UsosIncluidos, entity.AccesoIlimitado);
    }

    public async Task<IReadOnlyCollection<TarifaDto>> GetTarifasAsync(CancellationToken cancellationToken)
    {
        var empresaId = GetRequiredEmpresaId();

        var tarifas = await DbContext.TarifasProducto
            .AsNoTracking()
            .Join(DbContext.ProductosEmpresa.Where(p => p.EmpresaId == empresaId), x => x.ProductoEmpresaId, y => y.ProductoEmpresaId, (tarifa, producto) => new { tarifa, producto })
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

        var producto = await DbContext.ProductosEmpresa.FirstAsync(x => x.ProductoEmpresaId == request.ProductoEmpresaId && x.EmpresaId == empresaId, cancellationToken);

        var overlaps = await DbContext.TarifasProducto.AnyAsync(x =>
            x.ProductoEmpresaId == request.ProductoEmpresaId &&
            x.TipoClienteId == request.TipoClienteId &&
            x.TipoDia == request.TipoDia &&
            x.BloqueHorarioComercialId == request.BloqueHorarioComercialId &&
            x.VigenciaDesde <= request.VigenciaHasta &&
            request.VigenciaDesde <= x.VigenciaHasta,
            cancellationToken);

        if (overlaps)
        {
            throw new InvalidOperationException("Ya existe una tarifa solapada para esa combinación.");
        }

        var entity = new TarifaProducto
        {
            ProductoEmpresaId = request.ProductoEmpresaId,
            TipoClienteId = request.TipoClienteId,
            TipoDia = request.TipoDia,
            BloqueHorarioComercialId = request.BloqueHorarioComercialId,
            Precio = request.Precio,
            VigenciaDesde = request.VigenciaDesde,
            VigenciaHasta = request.VigenciaHasta,
            Activo = request.Activo,
            CreatedAt = DateTimeOffset.UtcNow
        };

        DbContext.TarifasProducto.Add(entity);
        await DbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync("tarifa_producto", entity.TarifaProductoId, "crear", request, empresaId, cancellationToken);

        var tipoClienteNombre = entity.TipoClienteId.HasValue
            ? await DbContext.TiposCliente.Where(x => x.TipoClienteId == entity.TipoClienteId).Select(x => x.Nombre).FirstOrDefaultAsync(cancellationToken)
            : null;

        return new TarifaDto(entity.TarifaProductoId, entity.ProductoEmpresaId, producto.NombreComercial, entity.TipoClienteId, tipoClienteNombre, entity.TipoDia, entity.BloqueHorarioComercialId, entity.Precio, entity.VigenciaDesde, entity.VigenciaHasta, entity.Activo);
    }

    public async Task<IReadOnlyCollection<ClaseDto>> GetClasesAsync(CancellationToken cancellationToken)
    {
        var empresaId = GetRequiredEmpresaId();

        return await DbContext.Clases
            .AsNoTracking()
            .Include(x => x.ProfesorEmpresa).ThenInclude(x => x.Persona)
            .Include(x => x.Horarios)
            .Where(x => x.EmpresaId == empresaId)
            .OrderBy(x => x.Nombre)
            .Select(x => new ClaseDto(
                x.ClaseId,
                x.Nombre,
                x.ProfesorEmpresaId,
                x.ProfesorEmpresa.Persona.NombreCompleto,
                x.CupoMaximo,
                x.Estado,
                x.Horarios.OrderBy(h => h.DiaSemana).ThenBy(h => h.HoraInicio)
                    .Select(h => new ClaseHorarioDto(h.ClaseHorarioId, h.DiaSemana, h.HoraInicio, h.HoraFin, h.Activo))
                    .ToArray()))
            .ToListAsync(cancellationToken);
    }

    public async Task<ClaseDto> CreateClaseAsync(UpsertClaseRequestDto request, CancellationToken cancellationToken)
    {
        var empresaId = GetRequiredEmpresaId();
        var profesor = await DbContext.ProfesoresEmpresa
            .Include(x => x.Persona)
            .FirstAsync(x => x.ProfesorEmpresaId == request.ProfesorEmpresaId && x.EmpresaId == empresaId, cancellationToken);

        var entity = new Clase
        {
            EmpresaId = empresaId,
            Nombre = request.Nombre,
            ProfesorEmpresaId = request.ProfesorEmpresaId,
            CupoMaximo = request.CupoMaximo,
            Estado = request.Estado,
            Horarios = request.Horarios.Select(h => new ClaseHorario
            {
                DiaSemana = h.DiaSemana,
                HoraInicio = h.HoraInicio,
                HoraFin = h.HoraFin,
                Activo = h.Activo
            }).ToList()
        };

        DbContext.Clases.Add(entity);
        await DbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync("clase", entity.ClaseId, "crear", request, empresaId, cancellationToken);

        return new ClaseDto(entity.ClaseId, entity.Nombre, entity.ProfesorEmpresaId, profesor.Persona.NombreCompleto, entity.CupoMaximo, entity.Estado, entity.Horarios.Select(h => new ClaseHorarioDto(h.ClaseHorarioId, h.DiaSemana, h.HoraInicio, h.HoraFin, h.Activo)).ToArray());
    }
}
