namespace ErpBoulder.Infrastructure.Services;

using ErpBoulder.Application.DTOs.Reportes;
using ErpBoulder.Application.Interfaces.Common;
using ErpBoulder.Application.Interfaces.Services;
using ErpBoulder.Domain.Constants;
using ErpBoulder.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public sealed class ReportesService : ServiceBase, IReportesService
{
    public ReportesService(ErpBoulderDbContext dbContext, ICurrentUserContext currentUser)
        : base(dbContext, currentUser)
    {
    }

    public async Task<DashboardReportDto> GetDashboardAsync(CancellationToken cancellationToken)
    {
        var empresaId = GetOptionalEmpresaId();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var todayDateTime = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);

        var ventasQuery = DbContext.Ventas.Where(x => x.Estado == "emitida");
        var clientesQuery = DbContext.ClientesEmpresa.Where(x => x.Estado == "activo");
        var beneficiosQuery = DbContext.BeneficiosCliente.Where(x => x.Estado == "vigente" && x.FechaInicio <= today && today <= x.FechaTermino);
        var accesosHoyQuery = DbContext.AccesoEventos.Where(x => x.Resultado == "autorizado" && x.FechaHora >= todayDateTime && x.FechaHora < todayDateTime.AddDays(1));

        if (empresaId.HasValue)
        {
            ventasQuery = ventasQuery.Where(x => x.EmpresaId == empresaId);
            clientesQuery = clientesQuery.Where(x => x.EmpresaId == empresaId);
            beneficiosQuery = beneficiosQuery.Where(x => x.EmpresaId == empresaId);
            accesosHoyQuery = accesosHoyQuery.Where(x => x.EmpresaId == empresaId);
        }

        var mensualidadTipos = await DbContext.TiposProductoBase
            .Where(x => x.Codigo == ProductBaseCodes.MensualidadPorHorario || x.Codigo == ProductBaseCodes.MensualidadTodoHorario || x.Codigo == ProductBaseCodes.Clases)
            .Select(x => x.TipoProductoBaseId)
            .ToListAsync(cancellationToken);

        var packTipoId = await DbContext.TiposProductoBase
            .Where(x => x.Codigo == ProductBaseCodes.PackTickets || x.Codigo == ProductBaseCodes.LegacyPack10Tickets)
            .Select(x => (long?)x.TipoProductoBaseId)
            .FirstOrDefaultAsync(cancellationToken);

        return new DashboardReportDto(
            await ventasQuery.SumAsync(x => (decimal?)x.Total, cancellationToken) ?? 0m,
            await ventasQuery.CountAsync(cancellationToken),
            await clientesQuery.CountAsync(cancellationToken),
            await beneficiosQuery.CountAsync(x => mensualidadTipos.Contains(x.TipoProductoBaseId), cancellationToken),
            packTipoId.HasValue ? await beneficiosQuery.CountAsync(x => x.TipoProductoBaseId == packTipoId.Value, cancellationToken) : 0,
            await accesosHoyQuery.CountAsync(cancellationToken));
    }

    public async Task<IReadOnlyCollection<SimpleReportItemDto>> GetVentasPorProductoAsync(CancellationToken cancellationToken)
    {
        var empresaId = GetOptionalEmpresaId();

        var query = DbContext.VentaDetalles
            .Join(DbContext.Ventas, d => d.VentaId, v => v.VentaId, (detalle, venta) => new { detalle, venta })
            .Where(x => x.venta.Estado == "emitida");

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

    public async Task<IReadOnlyCollection<SimpleReportItemDto>> GetVentasPorTipoClienteAsync(CancellationToken cancellationToken)
    {
        var empresaId = GetOptionalEmpresaId();

        var query = DbContext.Ventas
            .Where(x => x.Estado == "emitida" && x.ClienteEmpresaId.HasValue)
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

    public async Task<IReadOnlyCollection<SimpleReportItemDto>> GetAccesosPorBloqueAsync(CancellationToken cancellationToken)
    {
        var empresaId = GetOptionalEmpresaId();

        var query = DbContext.AccesoEventos
            .Where(x => x.Resultado == "autorizado")
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

    public async Task<IReadOnlyCollection<SimpleReportItemDto>> GetUsoClasesAsync(CancellationToken cancellationToken)
    {
        var empresaId = GetOptionalEmpresaId();

        var query = DbContext.ClaseAsistencias
            .Join(DbContext.ClaseSesiones, a => a.ClaseSesionId, s => s.ClaseSesionId, (asistencia, sesion) => new { asistencia, sesion })
            .Join(DbContext.Clases, x => x.sesion.ClaseId, c => c.ClaseId, (x, clase) => new { x.asistencia, x.sesion, clase });

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
}
