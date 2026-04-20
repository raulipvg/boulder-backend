namespace ErpBoulder.Infrastructure.Services;

using ErpBoulder.Application.DTOs.Reportes;
using ErpBoulder.Application.Interfaces.Common;
using ErpBoulder.Application.Interfaces.Services;
using ErpBoulder.Domain.Constants;
using ErpBoulder.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public sealed class ReportesService : ServiceBase, IReportesService
{
    private enum ReportePeriodo
    {
        Diario,
        Mensual,
        Anual,
    }

    private sealed record PeriodoFiltroContext(
        DateOnly FechaInicio,
        DateOnly FechaFin,
        DateTimeOffset FechaInicioUtc,
        DateTimeOffset FechaFinUtc);

    public ReportesService(ErpBoulderDbContext dbContext, ICurrentUserContext currentUser)
        : base(dbContext, currentUser)
    {
    }

    public async Task<DashboardReportDto> GetDashboardAsync(string? periodo, DateOnly? fechaReferencia, CancellationToken cancellationToken)
    {
        var empresaId = GetOptionalEmpresaId();
        var periodoContext = BuildPeriodoFiltroContext(periodo, fechaReferencia);

        var ventasQuery = DbContext.Ventas
            .Where(x => x.Estado == "emitida"
                && x.FechaHora >= periodoContext.FechaInicioUtc
                && x.FechaHora < periodoContext.FechaFinUtc);
        var clientesQuery = DbContext.ClientesEmpresa.Where(x => x.Estado == "activo");
        var beneficiosQuery = DbContext.BeneficiosCliente
            .Where(x => x.Estado == "vigente"
                && x.FechaInicio <= periodoContext.FechaFin
                && periodoContext.FechaInicio <= x.FechaTermino);
        var accesosQuery = DbContext.AccesoEventos
            .Where(x => x.Resultado == "autorizado"
                && x.FechaHora >= periodoContext.FechaInicioUtc
                && x.FechaHora < periodoContext.FechaFinUtc);

        if (empresaId.HasValue)
        {
            ventasQuery = ventasQuery.Where(x => x.EmpresaId == empresaId);
            clientesQuery = clientesQuery.Where(x => x.EmpresaId == empresaId);
            beneficiosQuery = beneficiosQuery.Where(x => x.EmpresaId == empresaId);
            accesosQuery = accesosQuery.Where(x => x.EmpresaId == empresaId);
        }

        var mensualidadTipos = await DbContext.TiposProductoBase
            .Where(x => x.Codigo == ProductBaseCodes.MensualidadPorHorario || x.Codigo == ProductBaseCodes.MensualidadTodoHorario || x.Codigo == ProductBaseCodes.Clases)
            .Select(x => x.TipoProductoBaseId)
            .ToListAsync(cancellationToken);

        var packTipoId = await DbContext.TiposProductoBase
            .Where(x => x.Codigo == ProductBaseCodes.PackTickets)
            .Select(x => (long?)x.TipoProductoBaseId)
            .FirstOrDefaultAsync(cancellationToken);

        return new DashboardReportDto(
            await ventasQuery.SumAsync(x => (decimal?)x.Total, cancellationToken) ?? 0m,
            await ventasQuery.CountAsync(cancellationToken),
            await clientesQuery.CountAsync(cancellationToken),
            await beneficiosQuery.CountAsync(x => mensualidadTipos.Contains(x.TipoProductoBaseId), cancellationToken),
            packTipoId.HasValue ? await beneficiosQuery.CountAsync(x => x.TipoProductoBaseId == packTipoId.Value, cancellationToken) : 0,
            await accesosQuery.CountAsync(cancellationToken));
    }

    public async Task<IReadOnlyCollection<SimpleReportItemDto>> GetVentasPorProductoAsync(string? periodo, DateOnly? fechaReferencia, CancellationToken cancellationToken)
    {
        var empresaId = GetOptionalEmpresaId();
        var periodoContext = BuildPeriodoFiltroContext(periodo, fechaReferencia);

        var query = DbContext.VentaDetalles
            .Join(DbContext.Ventas, d => d.VentaId, v => v.VentaId, (detalle, venta) => new { detalle, venta })
            .Where(x => x.venta.Estado == "emitida"
                && x.venta.FechaHora >= periodoContext.FechaInicioUtc
                && x.venta.FechaHora < periodoContext.FechaFinUtc);

        if (empresaId.HasValue)
        {
            query = query.Where(x => x.venta.EmpresaId == empresaId);
        }

        var data = await query.ToListAsync(cancellationToken);
        return data
            .GroupBy(x => x.detalle.ProductoNombreSnapshot)
            .Select(g => new SimpleReportItemDto(g.Key, g.Sum(x => x.detalle.Subtotal)))
            .OrderByDescending(x => x.Valor)
            .ToList();
    }

    public async Task<IReadOnlyCollection<SimpleReportItemDto>> GetVentasPorTipoClienteAsync(string? periodo, DateOnly? fechaReferencia, CancellationToken cancellationToken)
    {
        var empresaId = GetOptionalEmpresaId();
        var periodoContext = BuildPeriodoFiltroContext(periodo, fechaReferencia);

        var query = DbContext.Ventas
            .Where(x => x.Estado == "emitida"
                && x.ClienteEmpresaId.HasValue
                && x.FechaHora >= periodoContext.FechaInicioUtc
                && x.FechaHora < periodoContext.FechaFinUtc)
            .Join(DbContext.ClientesEmpresa.Include(c => c.TipoCliente), v => v.ClienteEmpresaId!.Value, c => c.ClienteEmpresaId, (venta, cliente) => new { venta, cliente });

        if (empresaId.HasValue)
        {
            query = query.Where(x => x.venta.EmpresaId == empresaId);
        }

        var data = await query.ToListAsync(cancellationToken);
        return data
            .GroupBy(x => x.cliente.TipoCliente.Nombre)
            .Select(g => new SimpleReportItemDto(g.Key, g.Sum(x => x.venta.Total)))
            .OrderByDescending(x => x.Valor)
            .ToList();
    }

    public async Task<IReadOnlyCollection<SimpleReportItemDto>> GetAccesosPorBloqueAsync(string? periodo, DateOnly? fechaReferencia, CancellationToken cancellationToken)
    {
        var empresaId = GetOptionalEmpresaId();
        var periodoContext = BuildPeriodoFiltroContext(periodo, fechaReferencia);

        var query = DbContext.AccesoEventos
            .Where(x => x.Resultado == "autorizado"
                && x.FechaHora >= periodoContext.FechaInicioUtc
                && x.FechaHora < periodoContext.FechaFinUtc)
            .Join(DbContext.BeneficiosCliente, a => a.BeneficioClienteId, b => b.BeneficioClienteId, (acceso, beneficio) => new { acceso, beneficio })
            .Join(DbContext.BloquesHorariosComerciales, x => x.beneficio.BloqueHorarioComercialId, b => b.BloqueHorarioComercialId, (x, bloque) => new { x.acceso, bloque });

        if (empresaId.HasValue)
        {
            query = query.Where(x => x.acceso.EmpresaId == empresaId);
        }

        var data = await query.ToListAsync(cancellationToken);
        return data
            .GroupBy(x => x.bloque.Nombre)
            .Select(g => new SimpleReportItemDto(g.Key, g.Count()))
            .OrderByDescending(x => x.Valor)
            .ToList();
    }

    public async Task<IReadOnlyCollection<SimpleReportItemDto>> GetUsoClasesAsync(string? periodo, DateOnly? fechaReferencia, CancellationToken cancellationToken)
    {
        var empresaId = GetOptionalEmpresaId();
        var periodoContext = BuildPeriodoFiltroContext(periodo, fechaReferencia);

        var query = DbContext.ClaseAsistencias
            .Join(DbContext.ClaseSesiones, a => a.ClaseSesionId, s => s.ClaseSesionId, (asistencia, sesion) => new { asistencia, sesion })
            .Join(DbContext.Clases, x => x.sesion.ClaseId, c => c.ClaseId, (x, clase) => new { x.asistencia, x.sesion, clase })
            .Where(x => x.sesion.Fecha >= periodoContext.FechaInicio && x.sesion.Fecha <= periodoContext.FechaFin);

        if (empresaId.HasValue)
        {
            query = query.Where(x => x.sesion.EmpresaId == empresaId);
        }

        var data = await query.ToListAsync(cancellationToken);
        return data
            .GroupBy(x => x.clase.Nombre)
            .Select(g => new SimpleReportItemDto(g.Key, g.Count()))
            .OrderByDescending(x => x.Valor)
            .ToList();
    }

    public async Task<IReadOnlyCollection<VentaReporteExportDto>> GetVentasExportAsync(string? periodo, DateOnly? fechaReferencia, CancellationToken cancellationToken)
    {
        var empresaId = GetOptionalEmpresaId();
        var periodoContext = BuildPeriodoFiltroContext(periodo, fechaReferencia);

        var query = DbContext.VentaDetalles
            .AsNoTracking()
            .Join(DbContext.Ventas.AsNoTracking(), d => d.VentaId, v => v.VentaId, (detalle, venta) => new { detalle, venta })
            .Where(x => x.venta.Estado == "emitida"
                && x.venta.FechaHora >= periodoContext.FechaInicioUtc
                && x.venta.FechaHora < periodoContext.FechaFinUtc);

        if (empresaId.HasValue)
        {
            query = query.Where(x => x.venta.EmpresaId == empresaId.Value);
        }

        return await query
            .OrderByDescending(x => x.venta.FechaHora)
            .ThenByDescending(x => x.detalle.VentaDetalleId)
            .Select(x => new VentaReporteExportDto(
                x.venta.VentaId,
                x.detalle.VentaDetalleId,
                x.venta.NumeroComprobante,
                x.venta.FechaHora,
                x.venta.ClienteEmpresaId.HasValue ? x.venta.ClienteEmpresa!.Persona.NombreCompleto : null,
                x.venta.ClienteEmpresaId.HasValue ? x.venta.ClienteEmpresa!.Persona.Rut : null,
                x.venta.ClienteEmpresaId.HasValue ? x.venta.ClienteEmpresa!.TipoCliente.Nombre : null,
                x.venta.UsuarioVendedor.Persona.NombreCompleto,
                x.detalle.ProductoNombreSnapshot,
                x.detalle.Cantidad,
                x.detalle.PrecioUnitario,
                x.detalle.Subtotal,
                x.venta.Total,
                x.venta.Estado))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<AccesoReporteExportDto>> GetAccesosExportAsync(string? periodo, DateOnly? fechaReferencia, CancellationToken cancellationToken)
    {
        var empresaId = GetOptionalEmpresaId();
        var periodoContext = BuildPeriodoFiltroContext(periodo, fechaReferencia);

        var accesosBase = DbContext.AccesoEventos
            .AsNoTracking()
            .Where(x => x.Resultado == "autorizado"
                && x.FechaHora >= periodoContext.FechaInicioUtc
                && x.FechaHora < periodoContext.FechaFinUtc);

        if (empresaId.HasValue)
        {
            accesosBase = accesosBase.Where(x => x.EmpresaId == empresaId.Value);
        }

        var query =
            from acceso in accesosBase
            join cliente in DbContext.ClientesEmpresa.AsNoTracking() on acceso.ClienteEmpresaId equals cliente.ClienteEmpresaId
            join personaCliente in DbContext.Personas.AsNoTracking() on cliente.PersonaId equals personaCliente.PersonaId
            join tipoCliente in DbContext.TiposCliente.AsNoTracking() on cliente.TipoClienteId equals tipoCliente.TipoClienteId
            join usuarioValidador in DbContext.Usuarios.AsNoTracking() on acceso.UsuarioValidadorId equals usuarioValidador.UsuarioId
            join personaValidador in DbContext.Personas.AsNoTracking() on usuarioValidador.PersonaId equals personaValidador.PersonaId
            join beneficioRow in DbContext.BeneficiosCliente.AsNoTracking() on acceso.BeneficioClienteId equals (long?)beneficioRow.BeneficioClienteId into beneficioLeft
            from beneficio in beneficioLeft.DefaultIfEmpty()
            join productoDirectoRow in DbContext.ProductosEmpresa.AsNoTracking() on acceso.ProductoEmpresaId equals (long?)productoDirectoRow.ProductoEmpresaId into productoDirectoLeft
            from productoDirecto in productoDirectoLeft.DefaultIfEmpty()
            join productoBeneficioRow in DbContext.ProductosEmpresa.AsNoTracking() on (beneficio != null ? (long?)beneficio.ProductoEmpresaId : null) equals (long?)productoBeneficioRow.ProductoEmpresaId into productoBeneficioLeft
            from productoBeneficio in productoBeneficioLeft.DefaultIfEmpty()
            join bloqueRow in DbContext.BloquesHorariosComerciales.AsNoTracking() on (beneficio != null ? beneficio.BloqueHorarioComercialId : null) equals (long?)bloqueRow.BloqueHorarioComercialId into bloqueLeft
            from bloque in bloqueLeft.DefaultIfEmpty()
            orderby acceso.FechaHora descending, acceso.AccesoEventoId descending
            select new AccesoReporteExportDto(
                acceso.AccesoEventoId,
                acceso.FechaHora,
                acceso.Resultado,
                acceso.MotivoRechazo,
                personaCliente.NombreCompleto,
                personaCliente.Rut,
                tipoCliente.Nombre,
                productoDirecto != null ? productoDirecto.NombreComercial : productoBeneficio != null ? productoBeneficio.NombreComercial : null,
                bloque != null ? bloque.Nombre : null,
                personaValidador.NombreCompleto);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ClaseReporteExportDto>> GetClasesExportAsync(string? periodo, DateOnly? fechaReferencia, CancellationToken cancellationToken)
    {
        var empresaId = GetOptionalEmpresaId();
        var periodoContext = BuildPeriodoFiltroContext(periodo, fechaReferencia);

        var sesionesBase = DbContext.ClaseSesiones
            .AsNoTracking()
            .Where(x => x.Fecha >= periodoContext.FechaInicio && x.Fecha <= periodoContext.FechaFin);

        if (empresaId.HasValue)
        {
            sesionesBase = sesionesBase.Where(x => x.EmpresaId == empresaId.Value);
        }

        var query =
            from asistencia in DbContext.ClaseAsistencias.AsNoTracking()
            join sesion in sesionesBase on asistencia.ClaseSesionId equals sesion.ClaseSesionId
            join clase in DbContext.Clases.AsNoTracking() on sesion.ClaseId equals clase.ClaseId
            join profesor in DbContext.ProfesoresEmpresa.AsNoTracking() on sesion.ProfesorEmpresaId equals profesor.ProfesorEmpresaId
            join personaProfesor in DbContext.Personas.AsNoTracking() on profesor.PersonaId equals personaProfesor.PersonaId
            join cliente in DbContext.ClientesEmpresa.AsNoTracking() on asistencia.ClienteEmpresaId equals cliente.ClienteEmpresaId
            join personaCliente in DbContext.Personas.AsNoTracking() on cliente.PersonaId equals personaCliente.PersonaId
            join tipoCliente in DbContext.TiposCliente.AsNoTracking() on cliente.TipoClienteId equals tipoCliente.TipoClienteId
            join beneficio in DbContext.BeneficiosCliente.AsNoTracking() on asistencia.BeneficioClienteId equals beneficio.BeneficioClienteId
            join producto in DbContext.ProductosEmpresa.AsNoTracking() on beneficio.ProductoEmpresaId equals producto.ProductoEmpresaId
            orderby sesion.Fecha descending, sesion.HoraInicio descending, asistencia.FechaHoraRegistro descending
            select new ClaseReporteExportDto(
                asistencia.ClaseAsistenciaId,
                asistencia.FechaHoraRegistro,
                sesion.Fecha,
                sesion.HoraInicio,
                sesion.HoraFin,
                clase.Nombre,
                personaProfesor.NombreCompleto,
                personaCliente.NombreCompleto,
                personaCliente.Rut,
                tipoCliente.Nombre,
                producto.NombreComercial,
                asistencia.Estado);

        return await query.ToListAsync(cancellationToken);
    }

    private static PeriodoFiltroContext BuildPeriodoFiltroContext(string? periodo, DateOnly? fechaReferencia)
    {
        var periodoNormalizado = ParsePeriodo(periodo);
        var fechaBase = fechaReferencia ?? GetChileTimeContext().TodayLocal;

        var fechaInicio = periodoNormalizado switch
        {
            ReportePeriodo.Diario => fechaBase,
            ReportePeriodo.Mensual => new DateOnly(fechaBase.Year, fechaBase.Month, 1),
            _ => new DateOnly(fechaBase.Year, 1, 1)
        };

        var fechaFin = periodoNormalizado switch
        {
            ReportePeriodo.Diario => fechaBase,
            ReportePeriodo.Mensual => new DateOnly(fechaBase.Year, fechaBase.Month, 1).AddMonths(1).AddDays(-1),
            _ => new DateOnly(fechaBase.Year, 12, 31)
        };

        var chileTimeZone = GetChileTimeZone();
        var fechaInicioLocal = DateTime.SpecifyKind(fechaInicio.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        var fechaFinLocalExclusiva = DateTime.SpecifyKind(fechaFin.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);

        var fechaInicioUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(fechaInicioLocal, chileTimeZone), TimeSpan.Zero);
        var fechaFinUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(fechaFinLocalExclusiva, chileTimeZone), TimeSpan.Zero);

        return new PeriodoFiltroContext(fechaInicio, fechaFin, fechaInicioUtc, fechaFinUtc);
    }

    private static ReportePeriodo ParsePeriodo(string? periodo)
    {
        if (string.IsNullOrWhiteSpace(periodo))
        {
            return ReportePeriodo.Diario;
        }

        return periodo.Trim().ToLowerInvariant() switch
        {
            "diario" => ReportePeriodo.Diario,
            "mensual" => ReportePeriodo.Mensual,
            "anual" => ReportePeriodo.Anual,
            _ => throw new InvalidOperationException($"El periodo '{periodo}' no es valido. Use diario, mensual o anual."),
        };
    }
}
