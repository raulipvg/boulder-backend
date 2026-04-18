namespace ErpBoulder.Infrastructure.Services;

using ErpBoulder.Application.DTOs.Operacion;
using ErpBoulder.Application.Interfaces.Common;
using ErpBoulder.Application.Interfaces.Services;
using ErpBoulder.Domain.Constants;
using ErpBoulder.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public sealed class OperacionService : ServiceBase, IOperacionService
{
    public OperacionService(ErpBoulderDbContext dbContext, ICurrentUserContext currentUser)
        : base(dbContext, currentUser)
    {
    }

    public async Task<IReadOnlyCollection<ClienteLookupDto>> BuscarClientesAsync(string? search, CancellationToken cancellationToken)
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
            .Take(20)
            .Select(x => new ClienteLookupDto(x.ClienteEmpresaId, x.Persona.NombreCompleto, x.Persona.Rut, x.Estado, x.TipoCliente.Nombre))
            .ToListAsync(cancellationToken);
    }

    public async Task<AccessPreviewDto> PrevisualizarAccesoAsync(long clienteEmpresaId, CancellationToken cancellationToken)
    {
        var empresaId = GetRequiredEmpresaId();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var cliente = await DbContext.ClientesEmpresa
            .AsNoTracking()
            .Include(x => x.Persona)
            .FirstOrDefaultAsync(x => x.EmpresaId == empresaId && x.ClienteEmpresaId == clienteEmpresaId, cancellationToken)
            ?? throw new InvalidOperationException("Cliente no encontrado.");

        var opciones = await DbContext.BeneficiosCliente
            .AsNoTracking()
            .Join(DbContext.ProductosEmpresa, b => b.ProductoEmpresaId, p => p.ProductoEmpresaId, (beneficio, producto) => new { beneficio, producto })
            .Where(x => x.beneficio.EmpresaId == empresaId
                && x.beneficio.ClienteEmpresaId == clienteEmpresaId
                && x.beneficio.Estado != "anulado"
                && x.beneficio.FechaInicio <= today
                && today <= x.beneficio.FechaTermino
                && (x.beneficio.UsosTotales == null || x.beneficio.AccesoIlimitado || x.beneficio.UsosConsumidos < x.beneficio.UsosTotales))
            .OrderBy(x => x.producto.NombreComercial)
            .Select(x => new AccessOptionDto(
                x.beneficio.BeneficioClienteId,
                x.producto.NombreComercial,
                x.beneficio.Estado,
                x.beneficio.FechaInicio,
                x.beneficio.FechaTermino,
                x.beneficio.UsosTotales,
                x.beneficio.UsosConsumidos))
            .ToListAsync(cancellationToken);

        return new AccessPreviewDto(cliente.ClienteEmpresaId, cliente.Persona.NombreCompleto, cliente.Estado, opciones);
    }

    public async Task<AccessValidationResultDto> ValidarAccesoAsync(ValidateAccessRequestDto request, CancellationToken cancellationToken)
    {
        var empresaId = GetRequiredEmpresaId();
        var usuarioId = GetRequiredUserId();
        var now = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-4));
        var today = DateOnly.FromDateTime(now.Date);
        var currentTime = TimeOnly.FromDateTime(now.DateTime);

        var cliente = await DbContext.ClientesEmpresa
            .AsNoTracking()
            .Include(x => x.Persona)
            .FirstOrDefaultAsync(x => x.EmpresaId == empresaId && x.ClienteEmpresaId == request.ClienteEmpresaId, cancellationToken)
            ?? throw new InvalidOperationException("Cliente no encontrado.");

        var beneficio = await DbContext.BeneficiosCliente
            .FirstOrDefaultAsync(x => x.EmpresaId == empresaId && x.ClienteEmpresaId == request.ClienteEmpresaId && x.BeneficioClienteId == request.BeneficioClienteId, cancellationToken)
            ?? throw new InvalidOperationException("Beneficio no encontrado.");

        var producto = await DbContext.ProductosEmpresa
            .Include(x => x.TipoProductoBase)
            .FirstAsync(x => x.ProductoEmpresaId == beneficio.ProductoEmpresaId, cancellationToken);

        var autorizado = true;
        string mensaje = "Acceso autorizado.";

        if (!string.Equals(cliente.Estado, "activo", StringComparison.OrdinalIgnoreCase))
        {
            autorizado = false;
            mensaje = "El cliente no está activo.";
        }
        else if (beneficio.Estado == "anulado" || beneficio.FechaInicio > today || today > beneficio.FechaTermino)
        {
            autorizado = false;
            mensaje = "El beneficio no está vigente.";
        }
        else if (!beneficio.AccesoIlimitado && beneficio.UsosTotales.HasValue && beneficio.UsosConsumidos >= beneficio.UsosTotales.Value)
        {
            autorizado = false;
            mensaje = "El beneficio ya no tiene saldo disponible.";
        }

        if (autorizado && beneficio.BloqueHorarioComercialId.HasValue)
        {
            var bloque = await DbContext.BloquesHorariosComerciales.FirstAsync(x => x.BloqueHorarioComercialId == beneficio.BloqueHorarioComercialId, cancellationToken);
            if (currentTime < bloque.HoraInicio || currentTime > bloque.HoraFin)
            {
                autorizado = false;
                mensaje = "Fuera del bloque horario autorizado.";
            }
        }

        if (autorizado && producto.TipoProductoBase.Codigo == ProductBaseCodes.Clases)
        {
            var dayOfWeek = now.DayOfWeek switch
            {
                DayOfWeek.Monday => (short)1,
                DayOfWeek.Tuesday => (short)2,
                DayOfWeek.Wednesday => (short)3,
                DayOfWeek.Thursday => (short)4,
                DayOfWeek.Friday => (short)5,
                DayOfWeek.Saturday => (short)6,
                _ => (short)7
            };

            var allowed = await DbContext.ClaseHorarios.AnyAsync(x =>
                x.ClaseId == beneficio.ClaseId
                && x.Activo
                && x.DiaSemana == dayOfWeek
                && x.HoraInicio <= currentTime
                && currentTime <= x.HoraFin,
                cancellationToken);

            if (!allowed)
            {
                autorizado = false;
                mensaje = "La clase no está autorizada en este horario.";
            }
        }

        var evento = new Domain.Entities.Operacion.AccesoEvento
        {
            EmpresaId = empresaId,
            ClienteEmpresaId = request.ClienteEmpresaId,
            BeneficioClienteId = beneficio.BeneficioClienteId,
            ProductoEmpresaId = producto.ProductoEmpresaId,
            UsuarioValidadorId = usuarioId,
            FechaHora = DateTimeOffset.UtcNow,
            Resultado = autorizado ? "autorizado" : "rechazado",
            MotivoRechazo = autorizado ? null : mensaje
        };

        DbContext.AccesoEventos.Add(evento);

        if (autorizado && producto.TipoProductoBase.Codigo != ProductBaseCodes.Clases)
        {
            beneficio.UsosConsumidos += 1;
            beneficio.UpdatedAt = DateTimeOffset.UtcNow;
            if (beneficio.UsosTotales.HasValue && beneficio.UsosConsumidos >= beneficio.UsosTotales.Value)
            {
                beneficio.Estado = "consumido";
            }
        }

        await DbContext.SaveChangesAsync(cancellationToken);

        return new AccessValidationResultDto(autorizado, mensaje, evento.AccesoEventoId, beneficio.BeneficioClienteId, producto.NombreComercial);
    }

    public async Task<IReadOnlyCollection<ClaseSesionDto>> GetSesionesAsync(DateOnly? fecha, CancellationToken cancellationToken)
    {
        var empresaId = GetRequiredEmpresaId();
        var targetDate = fecha ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);

        await EnsureSessionsForDateAsync(empresaId, targetDate, cancellationToken);

        return await DbContext.ClaseSesiones
            .AsNoTracking()
            .Join(DbContext.Clases, s => s.ClaseId, c => c.ClaseId, (sesion, clase) => new { sesion, clase })
            .Join(DbContext.ProfesoresEmpresa.Include(p => p.Persona), x => x.sesion.ProfesorEmpresaId, p => p.ProfesorEmpresaId, (x, profesor) => new { x.sesion, x.clase, profesor })
            .Where(x => x.sesion.EmpresaId == empresaId && x.sesion.Fecha == targetDate)
            .OrderBy(x => x.sesion.HoraInicio)
            .Select(x => new ClaseSesionDto(x.sesion.ClaseSesionId, x.sesion.Fecha, x.sesion.HoraInicio, x.sesion.HoraFin, x.clase.Nombre, x.profesor.Persona.NombreCompleto, x.sesion.CupoMaximo, x.sesion.Estado))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ClaseSesionInscritoDto>> GetInscritosSesionAsync(long claseSesionId, CancellationToken cancellationToken)
    {
        var empresaId = GetRequiredEmpresaId();

        var sesion = await DbContext.ClaseSesiones
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.EmpresaId == empresaId && x.ClaseSesionId == claseSesionId, cancellationToken)
            ?? throw new InvalidOperationException("Sesión no encontrada.");

        var asistenciaClienteIds = await DbContext.ClaseAsistencias
            .AsNoTracking()
            .Where(x => x.ClaseSesionId == claseSesionId)
            .Select(x => x.ClienteEmpresaId)
            .Distinct()
            .ToHashSetAsync(cancellationToken);

        var candidatos = await DbContext.BeneficiosCliente
            .AsNoTracking()
            .Join(DbContext.ClientesEmpresa.AsNoTracking().Include(x => x.Persona), beneficio => beneficio.ClienteEmpresaId, cliente => cliente.ClienteEmpresaId, (beneficio, cliente) => new { beneficio, cliente })
            .Join(DbContext.ProductosEmpresa.AsNoTracking(), x => x.beneficio.ProductoEmpresaId, producto => producto.ProductoEmpresaId, (x, producto) => new { x.beneficio, x.cliente, producto })
            .Where(x => x.beneficio.EmpresaId == empresaId
                && x.beneficio.ClaseId == sesion.ClaseId
                && x.beneficio.Estado != "anulado"
                && x.beneficio.FechaInicio <= sesion.Fecha
                && sesion.Fecha <= x.beneficio.FechaTermino
                && (x.beneficio.AccesoIlimitado || !x.beneficio.UsosTotales.HasValue || x.beneficio.UsosConsumidos < x.beneficio.UsosTotales.Value))
            .Select(x => new
            {
                x.beneficio.ClienteEmpresaId,
                ClienteNombre = x.cliente.Persona.NombreCompleto,
                x.cliente.Persona.Rut,
                EstadoCliente = x.cliente.Estado,
                x.beneficio.BeneficioClienteId,
                ProductoNombre = x.producto.NombreComercial,
                x.beneficio.UsosTotales,
                x.beneficio.UsosConsumidos,
                x.beneficio.AccesoIlimitado,
                x.beneficio.FechaTermino
            })
            .ToListAsync(cancellationToken);

        return candidatos
            .GroupBy(x => x.ClienteEmpresaId)
            .Select(group => group
                .OrderBy(x => x.FechaTermino)
                .ThenBy(x => x.BeneficioClienteId)
                .First())
            .OrderBy(x => x.ClienteNombre)
            .Select(x => new ClaseSesionInscritoDto(
                x.ClienteEmpresaId,
                x.ClienteNombre,
                x.Rut,
                x.EstadoCliente,
                x.BeneficioClienteId,
                x.ProductoNombre,
                x.UsosTotales,
                x.UsosConsumidos,
                x.AccesoIlimitado,
                asistenciaClienteIds.Contains(x.ClienteEmpresaId)))
            .ToList();
    }

    public async Task<ClaseAsistenciaDto> RegistrarAsistenciaAsync(RegisterAttendanceRequestDto request, CancellationToken cancellationToken)
    {
        var empresaId = GetRequiredEmpresaId();

        var sesion = await DbContext.ClaseSesiones.FirstOrDefaultAsync(x => x.EmpresaId == empresaId && x.ClaseSesionId == request.ClaseSesionId, cancellationToken)
            ?? throw new InvalidOperationException("Sesión no encontrada.");

        var beneficio = await DbContext.BeneficiosCliente.FirstOrDefaultAsync(x =>
            x.EmpresaId == empresaId
            && x.ClienteEmpresaId == request.ClienteEmpresaId
            && x.BeneficioClienteId == request.BeneficioClienteId,
            cancellationToken)
            ?? throw new InvalidOperationException("Beneficio no encontrado.");

        if (beneficio.ClaseId != sesion.ClaseId)
        {
            throw new InvalidOperationException("El beneficio no corresponde a la clase indicada.");
        }

        if (sesion.Fecha < beneficio.FechaInicio || sesion.Fecha > beneficio.FechaTermino)
        {
            throw new InvalidOperationException("La vigencia del beneficio no cubre la sesión.");
        }

        if (beneficio.UsosTotales.HasValue && beneficio.UsosConsumidos >= beneficio.UsosTotales.Value)
        {
            throw new InvalidOperationException("El beneficio ya no tiene clases disponibles.");
        }

        var exists = await DbContext.ClaseAsistencias.AnyAsync(x => x.ClaseSesionId == request.ClaseSesionId && x.ClienteEmpresaId == request.ClienteEmpresaId, cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException("La asistencia ya fue registrada.");
        }

        var asistencia = new Domain.Entities.Operacion.ClaseAsistencia
        {
            ClaseSesionId = request.ClaseSesionId,
            ClienteEmpresaId = request.ClienteEmpresaId,
            BeneficioClienteId = request.BeneficioClienteId,
            UsuarioRegistroId = GetRequiredUserId(),
            FechaHoraRegistro = DateTimeOffset.UtcNow,
            Estado = "asistio"
        };

        DbContext.ClaseAsistencias.Add(asistencia);

        beneficio.UsosConsumidos += 1;
        beneficio.UpdatedAt = DateTimeOffset.UtcNow;
        if (beneficio.UsosTotales.HasValue && beneficio.UsosConsumidos >= beneficio.UsosTotales.Value)
        {
            beneficio.Estado = "consumido";
        }

        await DbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync("clase_asistencia", asistencia.ClaseAsistenciaId, "crear", request, empresaId, cancellationToken);

        return new ClaseAsistenciaDto(asistencia.ClaseAsistenciaId, asistencia.ClaseSesionId, asistencia.ClienteEmpresaId, asistencia.Estado, asistencia.FechaHoraRegistro);
    }

    private async Task EnsureSessionsForDateAsync(long empresaId, DateOnly targetDate, CancellationToken cancellationToken)
    {
        var dayOfWeek = targetDate.DayOfWeek switch
        {
            DayOfWeek.Monday => (short)1,
            DayOfWeek.Tuesday => (short)2,
            DayOfWeek.Wednesday => (short)3,
            DayOfWeek.Thursday => (short)4,
            DayOfWeek.Friday => (short)5,
            DayOfWeek.Saturday => (short)6,
            _ => (short)7
        };

        var clases = await DbContext.Clases
            .Include(x => x.Horarios)
            .Where(x => x.EmpresaId == empresaId && x.Activo)
            .ToListAsync(cancellationToken);

        var existing = await DbContext.ClaseSesiones
            .Where(x => x.EmpresaId == empresaId && x.Fecha == targetDate)
            .Select(x => new { x.ClaseId, x.HoraInicio })
            .ToListAsync(cancellationToken);

        var hasChanges = false;

        foreach (var clase in clases)
        {
            foreach (var horario in clase.Horarios.Where(h => h.Activo && h.DiaSemana == dayOfWeek))
            {
                if (existing.Any(x => x.ClaseId == clase.ClaseId && x.HoraInicio == horario.HoraInicio))
                {
                    continue;
                }

                DbContext.ClaseSesiones.Add(new Domain.Entities.Operacion.ClaseSesion
                {
                    EmpresaId = empresaId,
                    ClaseId = clase.ClaseId,
                    Fecha = targetDate,
                    HoraInicio = horario.HoraInicio,
                    HoraFin = horario.HoraFin,
                    ProfesorEmpresaId = clase.ProfesorEmpresaId,
                    CupoMaximo = clase.CupoMaximo,
                    Estado = "programada",
                    CreatedAt = DateTimeOffset.UtcNow
                });

                hasChanges = true;
            }
        }

        if (hasChanges)
        {
            await DbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
