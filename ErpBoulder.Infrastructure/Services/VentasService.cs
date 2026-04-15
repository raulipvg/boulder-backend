namespace ErpBoulder.Infrastructure.Services;

using ErpBoulder.Application.DTOs.Ventas;
using ErpBoulder.Application.Interfaces.Common;
using ErpBoulder.Application.Interfaces.Services;
using ErpBoulder.Domain.Constants;
using ErpBoulder.Domain.Entities.Ventas;
using ErpBoulder.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public sealed class VentasService : ServiceBase, IVentasService
{
    public VentasService(ErpBoulderDbContext dbContext, ICurrentUserContext currentUser)
        : base(dbContext, currentUser)
    {
    }

    public async Task<IReadOnlyCollection<PosCatalogItemDto>> GetPosCatalogAsync(CancellationToken cancellationToken)
    {
        var empresaId = GetRequiredEmpresaId();

        return await DbContext.ProductosEmpresa
            .AsNoTracking()
            .Include(x => x.TipoProductoBase)
            .Where(x => x.EmpresaId == empresaId && x.Activo && x.VisiblePos)
            .OrderBy(x => x.NombreComercial)
            .Select(x => new PosCatalogItemDto(
                x.ProductoEmpresaId,
                x.NombreComercial,
                x.TipoProductoBase.Codigo,
                x.ModoPrecio,
                x.PrecioFijo,
                x.RequiereCliente,
                x.GeneraBeneficio,
                x.VisiblePos))
            .ToListAsync(cancellationToken);
    }

    public async Task<VentaPreviewDto> PreviewVentaAsync(PreviewVentaRequestDto request, CancellationToken cancellationToken)
    {
        var empresaId = GetRequiredEmpresaId();
        ClienteEmpresaInfo? clienteInfo = null;

        if (request.ClienteEmpresaId.HasValue)
        {
            clienteInfo = await DbContext.ClientesEmpresa
                .AsNoTracking()
                .Include(x => x.TipoCliente)
                .Where(x => x.EmpresaId == empresaId && x.ClienteEmpresaId == request.ClienteEmpresaId.Value)
                .Select(x => new ClienteEmpresaInfo(x.ClienteEmpresaId, x.Persona.NombreCompleto, x.TipoClienteId, x.TipoCliente.Nombre))
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("Cliente no encontrado.");
        }

        var productoIds = request.Items.Select(x => x.ProductoEmpresaId).Distinct().ToArray();
        var productos = await DbContext.ProductosEmpresa
            .Include(x => x.TipoProductoBase)
            .Where(x => x.EmpresaId == empresaId && productoIds.Contains(x.ProductoEmpresaId))
            .ToDictionaryAsync(x => x.ProductoEmpresaId, cancellationToken);

        var detalles = new List<VentaPreviewDetalleDto>();

        foreach (var item in request.Items)
        {
            var producto = productos[item.ProductoEmpresaId];
            var precio = await ResolvePriceAsync(producto, clienteInfo?.TipoClienteId, cancellationToken);
            detalles.Add(new VentaPreviewDetalleDto(producto.ProductoEmpresaId, producto.NombreComercial, item.Cantidad, precio, precio * item.Cantidad));
        }

        var subtotal = detalles.Sum(x => x.Subtotal);
        return new VentaPreviewDto(subtotal, subtotal, detalles);
    }

    public async Task<VentaDto> CreateVentaAsync(CreateVentaRequestDto request, CancellationToken cancellationToken)
    {
        var empresaId = GetRequiredEmpresaId();
        var usuarioId = GetRequiredUserId();

        if (request.Items.Count == 0)
        {
            throw new InvalidOperationException("Debe incluir al menos un item en la venta.");
        }

        ClienteEmpresaInfo? clienteInfo = null;
        if (request.ClienteEmpresaId.HasValue)
        {
            clienteInfo = await DbContext.ClientesEmpresa
                .AsNoTracking()
                .Include(x => x.Persona)
                .Include(x => x.TipoCliente)
                .Where(x => x.EmpresaId == empresaId && x.ClienteEmpresaId == request.ClienteEmpresaId.Value)
                .Select(x => new ClienteEmpresaInfo(x.ClienteEmpresaId, x.Persona.NombreCompleto, x.TipoClienteId, x.TipoCliente.Nombre))
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("Cliente no encontrado.");
        }

        var productoIds = request.Items.Select(x => x.ProductoEmpresaId).Distinct().ToArray();
        var productos = await DbContext.ProductosEmpresa
            .Include(x => x.TipoProductoBase)
            .Where(x => x.EmpresaId == empresaId && productoIds.Contains(x.ProductoEmpresaId))
            .ToDictionaryAsync(x => x.ProductoEmpresaId, cancellationToken);

        var venta = new Venta
        {
            EmpresaId = empresaId,
            ClienteEmpresaId = request.ClienteEmpresaId,
            UsuarioVendedorId = usuarioId,
            NumeroComprobante = GenerateComprobanteNumber(),
            FechaHora = DateTimeOffset.UtcNow,
            Estado = "emitida"
        };

        foreach (var item in request.Items)
        {
            if (!productos.TryGetValue(item.ProductoEmpresaId, out var producto))
            {
                throw new InvalidOperationException($"Producto {item.ProductoEmpresaId} no encontrado.");
            }

            if (producto.RequiereCliente && !request.ClienteEmpresaId.HasValue)
            {
                throw new InvalidOperationException($"El producto {producto.NombreComercial} requiere cliente.");
            }

            var precio = await ResolvePriceAsync(producto, clienteInfo?.TipoClienteId, cancellationToken);
            var detalle = new VentaDetalle
            {
                ProductoEmpresaId = producto.ProductoEmpresaId,
                Cantidad = item.Cantidad,
                PrecioUnitario = precio,
                Subtotal = precio * item.Cantidad,
                FechaInicioVigencia = item.FechaInicioVigencia,
                ProductoNombreSnapshot = producto.NombreComercial,
                Observacion = item.Observacion
            };

            venta.Detalles.Add(detalle);
        }

        venta.Subtotal = venta.Detalles.Sum(x => x.Subtotal);
        venta.Total = venta.Subtotal;

        var pagoTotal = request.Pagos.Sum(x => x.Monto);
        if (pagoTotal != venta.Total)
        {
            throw new InvalidOperationException("La suma de medios de pago debe coincidir con el total de la venta.");
        }

        foreach (var pago in request.Pagos)
        {
            venta.Pagos.Add(new VentaPago
            {
                MedioPagoId = pago.MedioPagoId,
                Monto = pago.Monto,
                Referencia = pago.Referencia
            });
        }

        await using var transaction = await DbContext.Database.BeginTransactionAsync(cancellationToken);
        DbContext.Ventas.Add(venta);
        await DbContext.SaveChangesAsync(cancellationToken);

        if (clienteInfo is not null)
        {
            foreach (var detalle in venta.Detalles)
            {
                var producto = productos[detalle.ProductoEmpresaId];
                if (!producto.GeneraBeneficio)
                {
                    continue;
                }

                var beneficio = await BuildBenefitAsync(empresaId, clienteInfo.ClienteEmpresaId, producto, detalle, cancellationToken);
                DbContext.BeneficiosCliente.Add(beneficio);
            }
        }

        await DbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await AuditAsync("venta", venta.VentaId, "crear", new { venta.NumeroComprobante, venta.Total }, empresaId, cancellationToken);

        return await GetVentaAsync(venta.VentaId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<VentaDto>> GetVentasAsync(CancellationToken cancellationToken)
    {
        var empresaId = GetRequiredEmpresaId();
        var ventas = await DbContext.Ventas
            .AsNoTracking()
            .Include(x => x.ClienteEmpresa).ThenInclude(x => x!.Persona)
            .Include(x => x.Detalles).ThenInclude(x => x.BeneficioCliente)
            .Include(x => x.Pagos).ThenInclude(x => x.MedioPago)
            .Where(x => x.EmpresaId == empresaId)
            .OrderByDescending(x => x.FechaHora)
            .Take(100)
            .ToListAsync(cancellationToken);

        return ventas.Select(MapVenta).ToList();
    }

    public async Task<VentaDto> AnularVentaAsync(long ventaId, CancelVentaRequestDto request, CancellationToken cancellationToken)
    {
        var empresaId = GetRequiredEmpresaId();
        var usuarioId = GetRequiredUserId();

        var venta = await DbContext.Ventas
            .Include(x => x.Detalles)
            .ThenInclude(x => x.BeneficioCliente)
            .FirstOrDefaultAsync(x => x.EmpresaId == empresaId && x.VentaId == ventaId, cancellationToken)
            ?? throw new InvalidOperationException("Venta no encontrada.");

        var beneficioIds = venta.Detalles.Where(x => x.BeneficioCliente is not null).Select(x => x.BeneficioCliente!.BeneficioClienteId).ToArray();
        var tieneUso = beneficioIds.Length > 0 && (
            await DbContext.AccesoEventos.AnyAsync(x => x.BeneficioClienteId.HasValue && beneficioIds.Contains(x.BeneficioClienteId.Value), cancellationToken) ||
            await DbContext.ClaseAsistencias.AnyAsync(x => beneficioIds.Contains(x.BeneficioClienteId), cancellationToken));

        if (tieneUso)
        {
            throw new InvalidOperationException("La venta ya tiene consumos asociados y no puede anularse.");
        }

        venta.Estado = "anulada";
        venta.MotivoAnulacion = request.Motivo;
        venta.AnuladaPorUsuarioId = usuarioId;
        venta.AnuladaAt = DateTimeOffset.UtcNow;

        foreach (var detalle in venta.Detalles.Where(x => x.BeneficioCliente is not null))
        {
            detalle.BeneficioCliente!.Estado = "anulado";
            detalle.BeneficioCliente.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await DbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync("venta", venta.VentaId, "anular", new { request.Motivo }, empresaId, cancellationToken);

        return await GetVentaAsync(venta.VentaId, cancellationToken);
    }

    private async Task<VentaDto> GetVentaAsync(long ventaId, CancellationToken cancellationToken)
    {
        var venta = await DbContext.Ventas
            .AsNoTracking()
            .Include(x => x.ClienteEmpresa).ThenInclude(x => x!.Persona)
            .Include(x => x.Detalles).ThenInclude(x => x.BeneficioCliente)
            .Include(x => x.Pagos).ThenInclude(x => x.MedioPago)
            .FirstAsync(x => x.VentaId == ventaId, cancellationToken);

        return MapVenta(venta);
    }

    private static VentaDto MapVenta(Venta venta)
    {
        return new VentaDto(
            venta.VentaId,
            venta.NumeroComprobante,
            venta.FechaHora,
            venta.Estado,
            venta.Subtotal,
            venta.Descuento,
            venta.Total,
            venta.ClienteEmpresaId,
            venta.ClienteEmpresa?.Persona?.NombreCompleto,
            venta.Detalles.Select(d => new VentaDetalleDto(
                d.VentaDetalleId,
                d.ProductoEmpresaId,
                d.ProductoNombreSnapshot,
                d.Cantidad,
                d.PrecioUnitario,
                d.Subtotal,
                d.FechaInicioVigencia,
                d.BeneficioCliente?.BeneficioClienteId)).ToArray(),
            venta.Pagos.Select(p => new VentaPagoDto(p.VentaPagoId, p.MedioPago.Nombre, p.Monto, p.Referencia)).ToArray(),
            venta.MotivoAnulacion);
    }

    private async Task<decimal> ResolvePriceAsync(Domain.Entities.Administracion.ProductoEmpresa producto, long? tipoClienteId, CancellationToken cancellationToken)
    {
        if (producto.ModoPrecio == "fijo")
        {
            return producto.PrecioFijo ?? 0m;
        }

        var now = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-4));
        var today = DateOnly.FromDateTime(now.Date);
        var tipoDia = await GetTipoDiaAsync(today, cancellationToken);

        var tarifa = await DbContext.TarifasProducto
            .Where(x => x.ProductoEmpresaId == producto.ProductoEmpresaId
                && x.Activo
                && x.VigenciaDesde <= today
                && today <= x.VigenciaHasta
                && x.TipoDia == tipoDia
                && x.TipoClienteId == tipoClienteId
                && x.BloqueHorarioComercialId == producto.BloqueHorarioComercialId)
            .OrderByDescending(x => x.VigenciaDesde)
            .FirstOrDefaultAsync(cancellationToken);

        if (tarifa is null)
        {
            throw new InvalidOperationException($"No existe una tarifa vigente para el producto {producto.NombreComercial}.");
        }

        return tarifa.Precio;
    }

    private async Task<BeneficioCliente> BuildBenefitAsync(long empresaId, long clienteEmpresaId, Domain.Entities.Administracion.ProductoEmpresa producto, VentaDetalle detalle, CancellationToken cancellationToken)
    {
        var fechaInicio = detalle.FechaInicioVigencia ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var fechaTermino = producto.VigenciaDias.HasValue
            ? fechaInicio.AddDays(producto.VigenciaDias.Value - 1)
            : fechaInicio;

        long? profesorEmpresaId = null;
        if (producto.ClaseId.HasValue)
        {
            profesorEmpresaId = await DbContext.Clases.Where(x => x.ClaseId == producto.ClaseId.Value).Select(x => (long?)x.ProfesorEmpresaId).FirstOrDefaultAsync(cancellationToken);
        }

        return new BeneficioCliente
        {
            VentaDetalleId = detalle.VentaDetalleId,
            EmpresaId = empresaId,
            ClienteEmpresaId = clienteEmpresaId,
            ProductoEmpresaId = producto.ProductoEmpresaId,
            TipoProductoBaseId = producto.TipoProductoBaseId,
            Estado = "vigente",
            FechaInicio = fechaInicio,
            FechaTermino = fechaTermino,
            UsosTotales = producto.UsosIncluidos.HasValue ? producto.UsosIncluidos.Value * detalle.Cantidad : null,
            UsosConsumidos = 0,
            AccesoIlimitado = producto.AccesoIlimitado,
            BloqueHorarioComercialId = producto.BloqueHorarioComercialId,
            ClaseId = producto.ClaseId,
            ProfesorEmpresaId = profesorEmpresaId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private sealed record ClienteEmpresaInfo(long ClienteEmpresaId, string NombreCompleto, long TipoClienteId, string TipoClienteNombre);
}
