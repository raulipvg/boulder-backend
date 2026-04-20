namespace ErpBoulder.Infrastructure.Services;

using ErpBoulder.Application.DTOs.Operacion;
using ErpBoulder.Application.Interfaces.Common;
using ErpBoulder.Application.Interfaces.Services;
using ErpBoulder.Domain.Constants;
using ErpBoulder.Domain.Entities.Ventas;
using ErpBoulder.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

public sealed class OperacionService : ServiceBase, IOperacionService
{
    private sealed record AccessBenefitCandidate(
        long BeneficioClienteId,
        long ProductoEmpresaId,
        string ProductoNombre,
        string TipoProductoBaseCodigo,
        string Estado,
        DateOnly FechaInicio,
        DateOnly FechaTermino,
        int? UsosTotales,
        int UsosConsumidos,
        bool AccesoIlimitado,
        long? BloqueHorarioComercialId,
        DateTimeOffset CreatedAt);

    private sealed record CommercialBlockWindow(TimeOnly HoraInicio, TimeOnly HoraFin);
    private sealed record ClassScheduleWindow(TimeOnly HoraInicio, TimeOnly HoraFin);

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
        var chileTime = GetChileTimeContext();

        var cliente = await DbContext.ClientesEmpresa
            .AsNoTracking()
            .Include(x => x.Persona)
            .FirstOrDefaultAsync(x => x.EmpresaId == empresaId && x.ClienteEmpresaId == clienteEmpresaId, cancellationToken)
            ?? throw new InvalidOperationException("Cliente no encontrado.");

        var opcionesBase = await DbContext.BeneficiosCliente
            .AsNoTracking()
            .Where(beneficio => beneficio.EmpresaId == empresaId
                && beneficio.ClienteEmpresaId == clienteEmpresaId
                && beneficio.Estado == "vigente"
                && beneficio.FechaInicio <= chileTime.TodayLocal
                && chileTime.TodayLocal <= beneficio.FechaTermino
                && (beneficio.AccesoIlimitado || beneficio.UsosTotales.HasValue && beneficio.UsosConsumidos < beneficio.UsosTotales.Value))
            .Join(
                DbContext.ProductosEmpresa.AsNoTracking(),
                beneficio => beneficio.ProductoEmpresaId,
                producto => producto.ProductoEmpresaId,
                (beneficio, producto) => new { beneficio, producto })
            .Join(
                DbContext.TiposProductoBase.AsNoTracking(),
                x => x.producto.TipoProductoBaseId,
                tipoProductoBase => tipoProductoBase.TipoProductoBaseId,
                (x, tipoProductoBase) => new { x.beneficio, x.producto, tipoProductoBase })
            .Where(x => x.tipoProductoBase.Codigo == ProductBaseCodes.TicketIndividual
                || x.tipoProductoBase.Codigo == ProductBaseCodes.PackTickets
                || x.tipoProductoBase.Codigo == ProductBaseCodes.MensualidadPorHorario
                || x.tipoProductoBase.Codigo == ProductBaseCodes.MensualidadTodoHorario)
            .Select(x => new AccessBenefitCandidate(
                x.beneficio.BeneficioClienteId,
                x.producto.ProductoEmpresaId,
                x.producto.NombreComercial,
                x.tipoProductoBase.Codigo,
                x.beneficio.Estado,
                x.beneficio.FechaInicio,
                x.beneficio.FechaTermino,
                x.beneficio.UsosTotales,
                x.beneficio.UsosConsumidos,
                x.beneficio.AccesoIlimitado,
                x.beneficio.BloqueHorarioComercialId,
                x.beneficio.CreatedAt))
            .ToListAsync(cancellationToken);

        var beneficioIds = opcionesBase
            .Select(x => x.BeneficioClienteId)
            .Distinct()
            .ToArray();

        var beneficiosValidadosHoy = beneficioIds.Length == 0
            ? new HashSet<long>()
            : await DbContext.AccesoEventos
                .AsNoTracking()
                .Where(x => x.EmpresaId == empresaId
                    && x.ClienteEmpresaId == clienteEmpresaId
                    && x.Resultado == "autorizado"
                    && x.BeneficioClienteId.HasValue
                    && beneficioIds.Contains(x.BeneficioClienteId.Value)
                    && x.FechaHora >= chileTime.StartOfDayUtc
                    && x.FechaHora < chileTime.EndOfDayUtc)
                .Select(x => x.BeneficioClienteId!.Value)
                .Distinct()
                .ToHashSetAsync(cancellationToken);

        var bloques = await GetActiveCommercialBlocksByIdsAsync(
            empresaId,
            opcionesBase.Where(x => x.BloqueHorarioComercialId.HasValue).Select(x => x.BloqueHorarioComercialId!.Value),
            cancellationToken);

        var clienteActivo = string.Equals(cliente.Estado, "activo", StringComparison.OrdinalIgnoreCase);

        var opciones = opcionesBase
            .OrderByDescending(x => IsAccessSpecific(x.TipoProductoBaseCodigo, x.BloqueHorarioComercialId))
            .ThenBy(x => x.FechaTermino)
            .ThenBy(x => GetRemainingUsesSortValue(x.AccesoIlimitado, x.UsosTotales, x.UsosConsumidos))
            .ThenBy(x => x.CreatedAt)
            .Select(option =>
            {
                var yaValidadoHoy = beneficiosValidadosHoy.Contains(option.BeneficioClienteId);
                var dentroBloqueHorario = IsWithinCommercialBlock(option.TipoProductoBaseCodigo, option.BloqueHorarioComercialId, chileTime.CurrentTimeLocal, bloques);
                var motivoNoValidable = GetAccessValidationError(
                    clienteActivo,
                    option.TipoProductoBaseCodigo,
                    option.Estado,
                    option.FechaInicio,
                    option.FechaTermino,
                    option.AccesoIlimitado,
                    option.UsosTotales,
                    option.UsosConsumidos,
                    option.BloqueHorarioComercialId,
                    yaValidadoHoy,
                    chileTime.TodayLocal,
                    chileTime.CurrentTimeLocal,
                    bloques);

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
        var chileTime = GetChileTimeContext();

        var cliente = await DbContext.ClientesEmpresa
            .AsNoTracking()
            .Include(x => x.Persona)
            .FirstOrDefaultAsync(x => x.EmpresaId == empresaId && x.ClienteEmpresaId == request.ClienteEmpresaId, cancellationToken)
            ?? throw new InvalidOperationException("Cliente no encontrado.");

        await using var transaction = await DbContext.Database.BeginTransactionAsync(cancellationToken);

        var beneficio = await GetLockedBenefitAsync(empresaId, request.ClienteEmpresaId, request.BeneficioClienteId, cancellationToken)
            ?? throw new InvalidOperationException("Beneficio no encontrado.");

        var producto = await DbContext.ProductosEmpresa
            .AsNoTracking()
            .Include(x => x.TipoProductoBase)
            .FirstAsync(x => x.EmpresaId == empresaId && x.ProductoEmpresaId == beneficio.ProductoEmpresaId, cancellationToken);

        var bloques = await GetActiveCommercialBlocksByIdsAsync(
            empresaId,
            beneficio.BloqueHorarioComercialId.HasValue ? [beneficio.BloqueHorarioComercialId.Value] : [],
            cancellationToken);

        var yaValidadoHoy = await HasAuthorizedAccessTodayAsync(
            empresaId,
            request.ClienteEmpresaId,
            beneficio.BeneficioClienteId,
            chileTime.StartOfDayUtc,
            chileTime.EndOfDayUtc,
            cancellationToken);

        var mensaje = GetAccessValidationError(
            string.Equals(cliente.Estado, "activo", StringComparison.OrdinalIgnoreCase),
            producto.TipoProductoBase.Codigo,
            beneficio.Estado,
            beneficio.FechaInicio,
            beneficio.FechaTermino,
            beneficio.AccesoIlimitado,
            beneficio.UsosTotales,
            beneficio.UsosConsumidos,
            beneficio.BloqueHorarioComercialId,
            yaValidadoHoy,
            chileTime.TodayLocal,
            chileTime.CurrentTimeLocal,
            bloques);

        if (mensaje is not null)
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
            MotivoRechazo = null,
        };

        DbContext.AccesoEventos.Add(evento);

        beneficio.UsosConsumidos += 1;
        beneficio.UpdatedAt = DateTimeOffset.UtcNow;

        if (!beneficio.AccesoIlimitado && beneficio.UsosTotales.HasValue && beneficio.UsosConsumidos >= beneficio.UsosTotales.Value)
        {
            beneficio.Estado = "consumido";
        }

        await DbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new AccessValidationResultDto(true, "Acceso autorizado.", evento.AccesoEventoId, beneficio.BeneficioClienteId, producto.NombreComercial);
    }

    public async Task<IReadOnlyCollection<ClaseSesionDto>> GetSesionesAsync(DateOnly? fecha, CancellationToken cancellationToken)
    {
        var empresaId = GetRequiredEmpresaId();
        var targetDate = fecha ?? GetChileTimeContext().TodayLocal;

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
            .Where(beneficio => beneficio.EmpresaId == empresaId
                && beneficio.ClaseId == sesion.ClaseId
                && beneficio.Estado == "vigente"
                && beneficio.FechaInicio <= sesion.Fecha
                && sesion.Fecha <= beneficio.FechaTermino
                && (beneficio.AccesoIlimitado || beneficio.UsosTotales.HasValue && beneficio.UsosConsumidos < beneficio.UsosTotales.Value))
            .Join(
                DbContext.ClientesEmpresa.AsNoTracking().Include(x => x.Persona),
                beneficio => beneficio.ClienteEmpresaId,
                cliente => cliente.ClienteEmpresaId,
                (beneficio, cliente) => new { beneficio, cliente })
            .Join(
                DbContext.ProductosEmpresa.AsNoTracking().Include(x => x.TipoProductoBase),
                x => x.beneficio.ProductoEmpresaId,
                producto => producto.ProductoEmpresaId,
                (x, producto) => new { x.beneficio, x.cliente, producto })
            .Where(x => x.producto.TipoProductoBase.Codigo == ProductBaseCodes.Clases)
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
                x.beneficio.FechaTermino,
                x.beneficio.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        return candidatos
            .GroupBy(x => x.ClienteEmpresaId)
            .Select(group => group
                .OrderBy(x => x.FechaTermino)
                .ThenBy(x => GetRemainingUsesSortValue(x.AccesoIlimitado, x.UsosTotales, x.UsosConsumidos))
                .ThenBy(x => x.CreatedAt)
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
        var chileTime = GetChileTimeContext();

        var sesion = await DbContext.ClaseSesiones
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.EmpresaId == empresaId && x.ClaseSesionId == request.ClaseSesionId, cancellationToken)
            ?? throw new InvalidOperationException("Sesión no encontrada.");

        var horariosActivos = await DbContext.ClaseHorarios
            .AsNoTracking()
            .Where(x => x.ClaseId == sesion.ClaseId && x.Activo && x.DiaSemana == chileTime.CurrentDayOfWeek)
            .Select(x => new ClassScheduleWindow(x.HoraInicio, x.HoraFin))
            .ToListAsync(cancellationToken);

        await using var transaction = await DbContext.Database.BeginTransactionAsync(cancellationToken);

        var beneficio = await GetLockedBenefitAsync(empresaId, request.ClienteEmpresaId, request.BeneficioClienteId, cancellationToken)
            ?? throw new InvalidOperationException("Beneficio no encontrado.");

        var tipoProductoBaseCodigo = await DbContext.TiposProductoBase
            .AsNoTracking()
            .Where(x => x.TipoProductoBaseId == beneficio.TipoProductoBaseId)
            .Select(x => x.Codigo)
            .FirstAsync(cancellationToken);

        var asistenciaYaRegistrada = await DbContext.ClaseAsistencias
            .AsNoTracking()
            .AnyAsync(x => x.ClaseSesionId == request.ClaseSesionId && x.ClienteEmpresaId == request.ClienteEmpresaId, cancellationToken);

        var mensaje = GetAttendanceValidationError(
            tipoProductoBaseCodigo,
            sesion,
            beneficio,
            chileTime,
            horariosActivos,
            asistenciaYaRegistrada);

        if (mensaje is not null)
        {
            throw new InvalidOperationException(mensaje);
        }

        var asistencia = new Domain.Entities.Operacion.ClaseAsistencia
        {
            ClaseSesionId = request.ClaseSesionId,
            ClienteEmpresaId = request.ClienteEmpresaId,
            BeneficioClienteId = request.BeneficioClienteId,
            UsuarioRegistroId = GetRequiredUserId(),
            FechaHoraRegistro = DateTimeOffset.UtcNow,
            Estado = "asistio",
        };

        DbContext.ClaseAsistencias.Add(asistencia);

        beneficio.UsosConsumidos += 1;
        beneficio.UpdatedAt = DateTimeOffset.UtcNow;

        if (!beneficio.AccesoIlimitado && beneficio.UsosTotales.HasValue && beneficio.UsosConsumidos >= beneficio.UsosTotales.Value)
        {
            beneficio.Estado = "consumido";
        }

        try
        {
            await DbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgresException
            && postgresException.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new InvalidOperationException("La asistencia ya fue registrada.");
        }

        await AuditAsync("clase_asistencia", asistencia.ClaseAsistenciaId, "crear", request, empresaId, cancellationToken);

        return new ClaseAsistenciaDto(asistencia.ClaseAsistenciaId, asistencia.ClaseSesionId, asistencia.ClienteEmpresaId, asistencia.Estado, asistencia.FechaHoraRegistro);
    }

    private async Task<BeneficioCliente?> GetLockedBenefitAsync(long empresaId, long clienteEmpresaId, long beneficioClienteId, CancellationToken cancellationToken)
    {
        return await DbContext.BeneficiosCliente
            .FromSqlInterpolated($@"
                select *
                from ventas.beneficio_cliente
                where empresa_id = {empresaId}
                  and cliente_empresa_id = {clienteEmpresaId}
                  and beneficio_cliente_id = {beneficioClienteId}
                for update")
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<Dictionary<long, CommercialBlockWindow>> GetActiveCommercialBlocksByIdsAsync(long empresaId, IEnumerable<long> blockIds, CancellationToken cancellationToken)
    {
        var ids = blockIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<long, CommercialBlockWindow>();
        }

        return await DbContext.BloquesHorariosComerciales
            .AsNoTracking()
            .Where(x => x.EmpresaId == empresaId && x.Activo && ids.Contains(x.BloqueHorarioComercialId))
            .Select(x => new { x.BloqueHorarioComercialId, x.HoraInicio, x.HoraFin })
            .ToDictionaryAsync(
                x => x.BloqueHorarioComercialId,
                x => new CommercialBlockWindow(x.HoraInicio, x.HoraFin),
                cancellationToken);
    }

    private async Task<bool> HasAuthorizedAccessTodayAsync(
        long empresaId,
        long clienteEmpresaId,
        long beneficioClienteId,
        DateTimeOffset startOfDayUtc,
        DateTimeOffset endOfDayUtc,
        CancellationToken cancellationToken)
    {
        return await DbContext.AccesoEventos
            .AsNoTracking()
            .AnyAsync(x =>
                x.EmpresaId == empresaId
                && x.ClienteEmpresaId == clienteEmpresaId
                && x.BeneficioClienteId == beneficioClienteId
                && x.Resultado == "autorizado"
                && x.FechaHora >= startOfDayUtc
                && x.FechaHora < endOfDayUtc,
                cancellationToken);
    }

    private static bool IsAccessGeneralProduct(string? tipoProductoBaseCodigo)
    {
        return string.Equals(tipoProductoBaseCodigo, ProductBaseCodes.TicketIndividual, StringComparison.OrdinalIgnoreCase)
            || string.Equals(tipoProductoBaseCodigo, ProductBaseCodes.PackTickets, StringComparison.OrdinalIgnoreCase)
            || string.Equals(tipoProductoBaseCodigo, ProductBaseCodes.MensualidadPorHorario, StringComparison.OrdinalIgnoreCase)
            || string.Equals(tipoProductoBaseCodigo, ProductBaseCodes.MensualidadTodoHorario, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasUsageAvailable(bool accesoIlimitado, int? usosTotales, int usosConsumidos)
    {
        return accesoIlimitado || usosTotales.HasValue && usosConsumidos < usosTotales.Value;
    }

    private static bool IsAccessSpecific(string tipoProductoBaseCodigo, long? bloqueHorarioComercialId)
    {
        return string.Equals(tipoProductoBaseCodigo, ProductBaseCodes.TicketIndividual, StringComparison.OrdinalIgnoreCase)
            || string.Equals(tipoProductoBaseCodigo, ProductBaseCodes.MensualidadPorHorario, StringComparison.OrdinalIgnoreCase)
            || string.Equals(tipoProductoBaseCodigo, ProductBaseCodes.PackTickets, StringComparison.OrdinalIgnoreCase) && bloqueHorarioComercialId.HasValue;
    }

    private static int GetRemainingUsesSortValue(bool accesoIlimitado, int? usosTotales, int usosConsumidos)
    {
        if (accesoIlimitado || !usosTotales.HasValue)
        {
            return int.MaxValue;
        }

        return Math.Max(usosTotales.Value - usosConsumidos, 0);
    }

    private static bool IsWithinCommercialBlock(
        string tipoProductoBaseCodigo,
        long? bloqueHorarioComercialId,
        TimeOnly currentTime,
        IReadOnlyDictionary<long, CommercialBlockWindow> activeBlocks)
    {
        return GetCommercialBlockValidationError(tipoProductoBaseCodigo, bloqueHorarioComercialId, currentTime, activeBlocks) is null;
    }

    private static string? GetCommercialBlockValidationError(
        string tipoProductoBaseCodigo,
        long? bloqueHorarioComercialId,
        TimeOnly currentTime,
        IReadOnlyDictionary<long, CommercialBlockWindow> activeBlocks)
    {
        if (string.Equals(tipoProductoBaseCodigo, ProductBaseCodes.MensualidadTodoHorario, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var requiresBlock = string.Equals(tipoProductoBaseCodigo, ProductBaseCodes.TicketIndividual, StringComparison.OrdinalIgnoreCase)
            || string.Equals(tipoProductoBaseCodigo, ProductBaseCodes.MensualidadPorHorario, StringComparison.OrdinalIgnoreCase);

        var validatesWhenConfigured = requiresBlock
            || string.Equals(tipoProductoBaseCodigo, ProductBaseCodes.PackTickets, StringComparison.OrdinalIgnoreCase) && bloqueHorarioComercialId.HasValue;

        if (!validatesWhenConfigured)
        {
            return null;
        }

        if (!bloqueHorarioComercialId.HasValue || !activeBlocks.TryGetValue(bloqueHorarioComercialId.Value, out var block))
        {
            return "Bloque horario no válido.";
        }

        if (currentTime < block.HoraInicio || currentTime > block.HoraFin)
        {
            return "Horario no permitido.";
        }

        return null;
    }

    private static string? GetAccessValidationError(
        bool clienteActivo,
        string tipoProductoBaseCodigo,
        string estado,
        DateOnly fechaInicio,
        DateOnly fechaTermino,
        bool accesoIlimitado,
        int? usosTotales,
        int usosConsumidos,
        long? bloqueHorarioComercialId,
        bool yaValidadoHoy,
        DateOnly today,
        TimeOnly currentTime,
        IReadOnlyDictionary<long, CommercialBlockWindow> activeBlocks)
    {
        if (!clienteActivo)
        {
            return "El cliente no está activo.";
        }

        if (!IsAccessGeneralProduct(tipoProductoBaseCodigo))
        {
            return "El beneficio no aplica para acceso general.";
        }

        if (!string.Equals(estado, "vigente", StringComparison.OrdinalIgnoreCase))
        {
            return "El beneficio no está vigente.";
        }

        if (fechaInicio > today || today > fechaTermino)
        {
            return "El beneficio está fuera de fecha.";
        }

        if (!HasUsageAvailable(accesoIlimitado, usosTotales, usosConsumidos))
        {
            return "El beneficio no tiene usos disponibles.";
        }

        if (yaValidadoHoy)
        {
            return "Este beneficio ya fue validado hoy.";
        }

        return GetCommercialBlockValidationError(tipoProductoBaseCodigo, bloqueHorarioComercialId, currentTime, activeBlocks);
    }

    private static string? GetAttendanceValidationError(
        string tipoProductoBaseCodigo,
        Domain.Entities.Operacion.ClaseSesion sesion,
        BeneficioCliente beneficio,
        ChileTimeContext chileTime,
        IReadOnlyCollection<ClassScheduleWindow> horariosActivos,
        bool asistenciaYaRegistrada)
    {
        if (sesion.Fecha != chileTime.TodayLocal)
        {
            return "La asistencia solo puede registrarse para sesiones del día actual.";
        }

        if (asistenciaYaRegistrada)
        {
            return "La asistencia ya fue registrada.";
        }

        if (!string.Equals(tipoProductoBaseCodigo, ProductBaseCodes.Clases, StringComparison.OrdinalIgnoreCase))
        {
            return "El beneficio no corresponde a la clase indicada.";
        }

        if (!string.Equals(beneficio.Estado, "vigente", StringComparison.OrdinalIgnoreCase))
        {
            return "El beneficio no está vigente.";
        }

        if (beneficio.FechaInicio > chileTime.TodayLocal || chileTime.TodayLocal > beneficio.FechaTermino)
        {
            return "La vigencia del beneficio no cubre la sesión.";
        }

        if (!HasUsageAvailable(beneficio.AccesoIlimitado, beneficio.UsosTotales, beneficio.UsosConsumidos))
        {
            return "El beneficio ya no tiene clases disponibles.";
        }

        if (beneficio.ClaseId != sesion.ClaseId)
        {
            return "El beneficio no corresponde a la clase indicada.";
        }

        if (horariosActivos.Count == 0)
        {
            return "La clase no tiene horario activo para el día actual.";
        }

        var withinWindow = horariosActivos.Any(h => IsWithinAttendanceWindow(chileTime.CurrentTimeLocal, h.HoraInicio, h.HoraFin));
        if (!withinWindow)
        {
            return "La validación está fuera de la ventana horaria permitida.";
        }

        return null;
    }

    private static bool IsWithinAttendanceWindow(TimeOnly currentTime, TimeOnly horaInicio, TimeOnly horaFin)
    {
        var startTicks = Math.Max(0, horaInicio.Ticks - TimeSpan.FromHours(1).Ticks);
        var endTicks = Math.Min(TimeOnly.MaxValue.Ticks, horaFin.Ticks + TimeSpan.FromHours(1).Ticks);

        var startWindow = TimeOnly.FromTimeSpan(TimeSpan.FromTicks(startTicks));
        var endWindow = TimeOnly.FromTimeSpan(TimeSpan.FromTicks(endTicks));

        return currentTime >= startWindow && currentTime <= endWindow;
    }

    private async Task EnsureSessionsForDateAsync(long empresaId, DateOnly targetDate, CancellationToken cancellationToken)
    {
        var dayOfWeek = GetDayOfWeekNumber(targetDate);

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
                    CreatedAt = DateTimeOffset.UtcNow,
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
