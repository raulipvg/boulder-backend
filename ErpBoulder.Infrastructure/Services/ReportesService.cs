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
