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
        var (_, today, currentTime, startOfDayUtc, endOfDayUtc) = GetChileAccessTimeContext();

        var cliente = await DbContext.ClientesEmpresa
            .AsNoTracking()
            .Include(x => x.Persona)
            .FirstOrDefaultAsync(x => x.EmpresaId == empresaId && x.ClienteEmpresaId == clienteEmpresaId, cancellationToken)
            ?? throw new InvalidOperationException("Cliente no encontrado.");

        var opcionesBase = await DbContext.BeneficiosCliente
            .AsNoTracking()
            .Join(DbContext.ProductosEmpresa, b => b.ProductoEmpresaId, p => p.ProductoEmpresaId, (beneficio, producto) => new { beneficio, producto })
            .Where(x => x.beneficio.EmpresaId == empresaId
                && x.beneficio.ClienteEmpresaId == clienteEmpresaId
                && x.beneficio.Estado != "anulado"
                && x.beneficio.FechaInicio <= today
                && today <= x.beneficio.FechaTermino
                && (x.beneficio.UsosTotales == null || x.beneficio.AccesoIlimitado || x.beneficio.UsosConsumidos < x.beneficio.UsosTotales))
            .OrderBy(x => x.producto.NombreComercial)
            .Select(x => new
            {
                x.beneficio.BeneficioClienteId,
                ProductoNombre = x.producto.NombreComercial,
                x.beneficio.Estado,
                x.beneficio.FechaInicio,
                x.beneficio.FechaTermino,
                x.beneficio.UsosTotales,
                x.beneficio.UsosConsumidos,
                x.beneficio.ClaseId,
                x.beneficio.BloqueHorarioComercialId
            })
            .ToListAsync(cancellationToken);

        var noClaseBeneficioIds = opcionesBase
            .Where(x => !x.ClaseId.HasValue)
            .Select(x => x.BeneficioClienteId)
            .Distinct()
            .ToArray();

        var claseBeneficioIds = opcionesBase
            .Where(x => x.ClaseId.HasValue)
            .Select(x => x.BeneficioClienteId)
            .Distinct()
            .ToArray();

        var beneficiosValidadosHoy = noClaseBeneficioIds.Length == 0
            ? new HashSet<long>()
            : await DbContext.AccesoEventos
                .AsNoTracking()
                .Where(x => x.EmpresaId == empresaId
                    && x.Resultado == "autorizado"
                    && x.BeneficioClienteId.HasValue
                    && noClaseBeneficioIds.Contains(x.BeneficioClienteId.Value)
                    && x.FechaHora >= startOfDayUtc
                    && x.FechaHora < endOfDayUtc)
                .Select(x => x.BeneficioClienteId!.Value)
                .Distinct()
                .ToHashSetAsync(cancellationToken);

        var bloqueIds = opcionesBase
            .Where(x => !x.ClaseId.HasValue && x.BloqueHorarioComercialId.HasValue)
            .Select(x => x.BloqueHorarioComercialId!.Value)
            .Distinct()
            .ToArray();

        var beneficiosConAsistenciaHoy = claseBeneficioIds.Length == 0
            ? new HashSet<long>()
            : await DbContext.ClaseAsistencias
                .AsNoTracking()
                .Join(DbContext.ClaseSesiones.AsNoTracking(), a => a.ClaseSesionId, s => s.ClaseSesionId, (asistencia, sesion) => new { asistencia, sesion })
                .Where(x => x.sesion.EmpresaId == empresaId
                    && x.sesion.Fecha == today
                    && x.asistencia.ClienteEmpresaId == clienteEmpresaId
                    && claseBeneficioIds.Contains(x.asistencia.BeneficioClienteId))
                .Select(x => x.asistencia.BeneficioClienteId)
                .Distinct()
                .ToHashSetAsync(cancellationToken);

        var bloques = bloqueIds.Length == 0
            ? new Dictionary<long, (TimeOnly HoraInicio, TimeOnly HoraFin)>()
            : await DbContext.BloquesHorariosComerciales
                .AsNoTracking()
                .Where(x => bloqueIds.Contains(x.BloqueHorarioComercialId))
                .Select(x => new { x.BloqueHorarioComercialId, x.HoraInicio, x.HoraFin })
                .ToDictionaryAsync(x => x.BloqueHorarioComercialId, x => (x.HoraInicio, x.HoraFin), cancellationToken);

        var opciones = opcionesBase
            .Select(option =>
            {
                if (option.ClaseId.HasValue)
                {
                    var asistenciaYaRegistradaHoy = beneficiosConAsistenciaHoy.Contains(option.BeneficioClienteId);
                    return new AccessOptionDto(
                        option.BeneficioClienteId,
                        option.ProductoNombre,
                        option.Estado,
                        option.FechaInicio,
                        option.FechaTermino,
                        option.UsosTotales,
                        option.UsosConsumidos,
                        !asistenciaYaRegistradaHoy,
                        false,
                        true,
                        asistenciaYaRegistradaHoy ? "La asistencia de clase ya fue registrada hoy." : null);
                }

                var yaValidadoHoy = beneficiosValidadosHoy.Contains(option.BeneficioClienteId);
                var dentroBloqueHorario = option.BloqueHorarioComercialId.HasValue
                    && bloques.TryGetValue(option.BloqueHorarioComercialId.Value, out var bloque)
                    && bloque.HoraInicio <= currentTime
                    && currentTime <= bloque.HoraFin;

                string? motivoNoValidable = null;
                if (!option.BloqueHorarioComercialId.HasValue || !bloques.ContainsKey(option.BloqueHorarioComercialId.Value))
                {
                    motivoNoValidable = "El beneficio no tiene bloque horario configurado.";
                }
                else if (!dentroBloqueHorario)
                {
                    motivoNoValidable = "Fuera del bloque horario autorizado.";
                }

                if (yaValidadoHoy)
                {
                    motivoNoValidable = "Este beneficio ya fue validado hoy.";
                }

                return new AccessOptionDto(
                    option.BeneficioClienteId,
                    option.ProductoNombre,
                    option.Estado,
                    option.FechaInicio,
                    option.FechaTermino,
                    option.UsosTotales,
                    option.UsosConsumidos,
                    motivoNoValidable is null,
                    yaValidadoHoy,
                    dentroBloqueHorario,
                    motivoNoValidable);
            })
            .ToList();

        return new AccessPreviewDto(cliente.ClienteEmpresaId, cliente.Persona.NombreCompleto, cliente.Estado, opciones);
    }

    public async Task<AccessValidationResultDto> ValidarAccesoAsync(ValidateAccessRequestDto request, CancellationToken cancellationToken)
    {
        var empresaId = GetRequiredEmpresaId();
        var usuarioId = GetRequiredUserId();
        var (_, today, currentTime, startOfDayUtc, endOfDayUtc) = GetChileAccessTimeContext();

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

        if (autorizado && beneficio.ClaseId.HasValue)
        {
            var asistenciaYaRegistradaHoy = await DbContext.ClaseAsistencias
                .AsNoTracking()
                .Join(DbContext.ClaseSesiones.AsNoTracking(), a => a.ClaseSesionId, s => s.ClaseSesionId, (asistencia, sesion) => new { asistencia, sesion })
                .AnyAsync(x => x.sesion.EmpresaId == empresaId
                    && x.sesion.Fecha == today
                    && x.asistencia.ClienteEmpresaId == request.ClienteEmpresaId
                    && x.asistencia.BeneficioClienteId == beneficio.BeneficioClienteId,
                    cancellationToken);

            if (asistenciaYaRegistradaHoy)
            {
                autorizado = false;
                mensaje = "La asistencia de clase ya fue registrada hoy.";
            }
        }

        if (autorizado && !beneficio.ClaseId.HasValue)
        {
            if (!beneficio.BloqueHorarioComercialId.HasValue)
            {
                autorizado = false;
                mensaje = "El beneficio no tiene bloque horario configurado.";
            }
            else
            {
                var bloque = await DbContext.BloquesHorariosComerciales
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.BloqueHorarioComercialId == beneficio.BloqueHorarioComercialId.Value, cancellationToken);

                if (bloque is null)
                {
                    autorizado = false;
                    mensaje = "El beneficio no tiene bloque horario configurado.";
                }
                else if (currentTime < bloque.HoraInicio || currentTime > bloque.HoraFin)
                {
                    autorizado = false;
                    mensaje = "Fuera del bloque horario autorizado.";
                }
            }
        }

        if (autorizado && !beneficio.ClaseId.HasValue)
        {
            var accesoAutorizadoHoy = await DbContext.AccesoEventos
                .AsNoTracking()
                .AnyAsync(x =>
                    x.EmpresaId == empresaId
                    && x.ClienteEmpresaId == request.ClienteEmpresaId
                    && x.BeneficioClienteId == beneficio.BeneficioClienteId
                    && x.Resultado == "autorizado"
                    && x.FechaHora >= startOfDayUtc
                    && x.FechaHora < endOfDayUtc,
                    cancellationToken);

            if (accesoAutorizadoHoy)
            {
                autorizado = false;
                mensaje = "Este beneficio ya fue validado hoy.";
            }
        }

        if (!autorizado)
        {
            return new AccessValidationResultDto(false, mensaje, null, beneficio.BeneficioClienteId, producto.NombreComercial);
        }

        var evento = new Domain.Entities.Operacion.AccesoEvento
        {
            EmpresaId = empresaId,
            ClienteEmpresaId = request.ClienteEmpresaId,
            BeneficioClienteId = beneficio.BeneficioClienteId,
            ProductoEmpresaId = producto.ProductoEmpresaId,
            UsuarioValidadorId = usuarioId,
            FechaHora = DateTimeOffset.UtcNow,
            Resultado = "autorizado",
            MotivoRechazo = null
        };

        DbContext.AccesoEventos.Add(evento);

        if (producto.TipoProductoBase.Codigo != ProductBaseCodes.Clases)
        {
            beneficio.UsosConsumidos += 1;
            beneficio.UpdatedAt = DateTimeOffset.UtcNow;
            if (beneficio.UsosTotales.HasValue && beneficio.UsosConsumidos >= beneficio.UsosTotales.Value)
            {
                beneficio.Estado = "consumido";
            }
        }

        await DbContext.SaveChangesAsync(cancellationToken);

        return new AccessValidationResultDto(true, mensaje, evento.AccesoEventoId, beneficio.BeneficioClienteId, producto.NombreComercial);
    }

    private static TimeZoneInfo GetChileTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Santiago");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Pacific SA Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                throw new InvalidOperationException("No se encontró la zona horaria de Chile en el sistema.");
            }
            catch (InvalidTimeZoneException)
            {
                throw new InvalidOperationException("La zona horaria de Chile es inválida en el sistema.");
            }
        }
        catch (InvalidTimeZoneException)
        {
            throw new InvalidOperationException("La zona horaria de Chile es inválida en el sistema.");
        }
    }

    private static (DateTimeOffset NowLocal, DateOnly TodayLocal, TimeOnly CurrentTimeLocal, DateTimeOffset StartOfDayUtc, DateTimeOffset EndOfDayUtc) GetChileAccessTimeContext()
    {
        var chileTimeZone = GetChileTimeZone();
        var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, chileTimeZone);

        var localDate = nowLocal.Date;
        var startOfDayLocal = DateTime.SpecifyKind(localDate, DateTimeKind.Unspecified);
        var endOfDayLocal = startOfDayLocal.AddDays(1);

        var startOfDayUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(startOfDayLocal, chileTimeZone), TimeSpan.Zero);
        var endOfDayUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(endOfDayLocal, chileTimeZone), TimeSpan.Zero);

        return (
            nowLocal,
            DateOnly.FromDateTime(nowLocal.DateTime),
            TimeOnly.FromDateTime(nowLocal.DateTime),
            startOfDayUtc,
            endOfDayUtc);
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
