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
    private const string TipoClienteGeneralCode = "GENERAL";
    private const string TipoClienteEstudianteCode = "ESTUDIANTE";

    private sealed record PosCatalogProductInfo(
        long ProductoEmpresaId,
        string NombreComercial,
        string TipoProductoBaseCodigo,
        string ModoPrecio,
        decimal? PrecioFijo,
        bool RequiereCliente,
        bool GeneraBeneficio,
        bool VisiblePos,
        long? ClaseId,
        long? BloqueHorarioComercialId);

    private sealed record TarifaContext(DateOnly Today, string TipoDia, HashSet<long> ActiveBloqueIds);

    private sealed record TarifaCandidate(
        long ProductoEmpresaId,
        string TipoClienteCodigo,
        decimal Precio,
        DateOnly VigenciaDesde,
        string? TipoDia,
        long? BloqueHorarioComercialId);

    private sealed record BloqueCatalogInfo(string Nombre, TimeOnly HoraInicio, TimeOnly HoraFin);

    public VentasService(ErpBoulderDbContext dbContext, ICurrentUserContext currentUser)
        : base(dbContext, currentUser)
    {
    }

    public async Task<IReadOnlyCollection<PosCatalogItemDto>> GetPosCatalogAsync(CancellationToken cancellationToken)
    {
        var empresaId = GetRequiredEmpresaId();

        var productos = await DbContext.ProductosEmpresa
            .AsNoTracking()
            .Include(x => x.TipoProductoBase)
            .Where(x => x.EmpresaId == empresaId && x.Activo && x.VisiblePos && (x.ModoPrecio != "tarifa" || x.TarifaAsociada))
            .OrderBy(x => x.NombreComercial)
            .Select(x => new PosCatalogProductInfo(
                x.ProductoEmpresaId,
                x.NombreComercial,
                x.TipoProductoBase.Codigo,
                x.ModoPrecio,
                x.PrecioFijo,
                x.RequiereCliente,
                x.GeneraBeneficio,
                x.VisiblePos,
                x.ClaseId,
                x.BloqueHorarioComercialId))
            .ToListAsync(cancellationToken);

        var tarifaProductIds = productos
            .Where(x => IsPosTarifaPreviewProductCode(x.TipoProductoBaseCodigo))
            .Select(x => x.ProductoEmpresaId)
            .ToArray();

        var classIds = productos
            .Where(x => x.TipoProductoBaseCodigo == ProductBaseCodes.Clases && x.ClaseId.HasValue)
            .Select(x => x.ClaseId!.Value)
            .Distinct()
            .ToArray();

        var classDaysByClaseId = await BuildClassDaysByClaseIdAsync(classIds, cancellationToken);
        var tarifasByProduct = await BuildCatalogTarifasByProductAsync(empresaId, productos, tarifaProductIds, cancellationToken);

        return productos
            .Select(x =>
            {
                var tarifas = tarifasByProduct.GetValueOrDefault(x.ProductoEmpresaId);
                var diasClase = x.ClaseId.HasValue && classDaysByClaseId.TryGetValue(x.ClaseId.Value, out var days)
                    ? days
                    : Array.Empty<string>();

                return new PosCatalogItemDto(
                x.ProductoEmpresaId,
                x.NombreComercial,
                x.TipoProductoBaseCodigo,
                x.ModoPrecio,
                x.PrecioFijo,
                x.RequiereCliente,
                x.GeneraBeneficio,
                x.VisiblePos,
                tarifas.General,
                tarifas.Estudiante,
                tarifas.GeneralBloque,
                tarifas.EstudianteBloque,
                diasClase);
            })
            .ToList();
    }

    private async Task<Dictionary<long, IReadOnlyCollection<string>>> BuildClassDaysByClaseIdAsync(long[] classIds, CancellationToken cancellationToken)
    {
        if (classIds.Length == 0)
        {
            return new Dictionary<long, IReadOnlyCollection<string>>();
        }

        var classScheduleRows = await DbContext.ClaseHorarios
            .AsNoTracking()
            .Where(x => classIds.Contains(x.ClaseId) && x.Activo)
            .Select(x => new { x.ClaseId, x.DiaSemana })
            .ToListAsync(cancellationToken);

        return classScheduleRows
            .GroupBy(x => x.ClaseId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<string>)group
                    .OrderBy(x => x.DiaSemana)
                    .Select(x => ToDayCode(x.DiaSemana))
                    .Distinct()
                    .ToArray());
    }

    private async Task<Dictionary<long, (decimal? General, decimal? Estudiante, string? GeneralBloque, string? EstudianteBloque)>> BuildCatalogTarifasByProductAsync(
        long empresaId,
        IReadOnlyCollection<PosCatalogProductInfo> productos,
        long[] tarifaProductIds,
        CancellationToken cancellationToken)
    {
        if (tarifaProductIds.Length == 0)
        {
            return new Dictionary<long, (decimal? General, decimal? Estudiante, string? GeneralBloque, string? EstudianteBloque)>();
        }

        var context = await BuildTarifaContextAsync(empresaId, cancellationToken);
        var productById = productos.ToDictionary(x => x.ProductoEmpresaId, x => x);

        var tarifasCandidatas = await (
            from tarifa in DbContext.TarifasProducto.AsNoTracking()
            join tipoCliente in DbContext.TiposCliente.AsNoTracking() on tarifa.TipoClienteId equals tipoCliente.TipoClienteId
            where tarifaProductIds.Contains(tarifa.ProductoEmpresaId)
                && tarifa.Activo
                && tarifa.VigenciaDesde <= context.Today
                && context.Today <= tarifa.VigenciaHasta
                && (tipoCliente.Codigo == TipoClienteGeneralCode || tipoCliente.Codigo == TipoClienteEstudianteCode)
            select new TarifaCandidate(
                tarifa.ProductoEmpresaId,
                tipoCliente.Codigo,
                tarifa.Precio,
                tarifa.VigenciaDesde,
                tarifa.TipoDia,
                tarifa.BloqueHorarioComercialId))
            .ToListAsync(cancellationToken);

        var bloqueIds = tarifasCandidatas
            .Where(x => x.BloqueHorarioComercialId.HasValue)
            .Select(x => x.BloqueHorarioComercialId!.Value)
            .Distinct()
            .ToArray();

        var bloquesById = bloqueIds.Length == 0
            ? new Dictionary<long, BloqueCatalogInfo>()
            : await DbContext.BloquesHorariosComerciales
                .AsNoTracking()
                .Where(x => x.EmpresaId == empresaId && bloqueIds.Contains(x.BloqueHorarioComercialId))
                .Select(x => new { x.BloqueHorarioComercialId, x.Nombre, x.HoraInicio, x.HoraFin })
                .ToDictionaryAsync(
                    x => x.BloqueHorarioComercialId,
                    x => new BloqueCatalogInfo(x.Nombre, x.HoraInicio, x.HoraFin),
                    cancellationToken);

        return tarifasCandidatas
            .Where(candidato =>
                productById.TryGetValue(candidato.ProductoEmpresaId, out var product)
                && IsTarifaCandidateValidForProduct(product, candidato, context))
            .GroupBy(candidato => candidato.ProductoEmpresaId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var product = productById[group.Key];
                    var generalTarifa = SelectTarifaCandidate(group, product, TipoClienteGeneralCode);
                    var estudianteTarifa = SelectTarifaCandidate(group, product, TipoClienteEstudianteCode);

                    return (
                        generalTarifa?.Precio,
                        estudianteTarifa?.Precio,
                        BuildBloqueLabel(generalTarifa, bloquesById),
                        BuildBloqueLabel(estudianteTarifa, bloquesById));
                });
    }

    private async Task<TarifaContext> BuildTarifaContextAsync(long empresaId, CancellationToken cancellationToken)
    {
        var chileTime = GetChileTimeContext();
        var today = chileTime.TodayLocal;
        var currentTime = chileTime.CurrentTimeLocal;
        var tipoDia = await GetTipoDiaAsync(today, cancellationToken);

        var activeBloqueIds = await DbContext.BloquesHorariosComerciales
            .AsNoTracking()
            .Where(x => x.EmpresaId == empresaId
                && x.Activo
                && x.HoraInicio <= currentTime
                && currentTime < x.HoraFin)
            .Select(x => x.BloqueHorarioComercialId)
            .ToArrayAsync(cancellationToken);

        return new TarifaContext(today, tipoDia, activeBloqueIds.ToHashSet());
    }

    private static bool IsPosTarifaPreviewProductCode(string tipoProductoBaseCodigo)
    {
        return string.Equals(tipoProductoBaseCodigo, ProductBaseCodes.Clases, StringComparison.OrdinalIgnoreCase)
            || IsHorarioTarifaProductCode(tipoProductoBaseCodigo);
    }

    private static bool IsHorarioTarifaProductCode(string tipoProductoBaseCodigo)
    {
        return IsCurrentTimeTarifaProductCode(tipoProductoBaseCodigo)
            || IsPackTarifaProductCode(tipoProductoBaseCodigo)
            || string.Equals(tipoProductoBaseCodigo, ProductBaseCodes.MensualidadPorHorario, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCurrentTimeTarifaProductCode(string tipoProductoBaseCodigo)
    {
        return string.Equals(tipoProductoBaseCodigo, ProductBaseCodes.TicketIndividual, StringComparison.OrdinalIgnoreCase)
            || string.Equals(tipoProductoBaseCodigo, ProductBaseCodes.MensualidadTodoHorario, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPackTarifaProductCode(string tipoProductoBaseCodigo)
    {
        return ProductBaseCodes.IsPackTickets(tipoProductoBaseCodigo);
    }

    private static bool IsTarifaCandidateValidForProduct(PosCatalogProductInfo product, TarifaCandidate candidate, TarifaContext context)
    {
        if (string.Equals(product.TipoProductoBaseCodigo, ProductBaseCodes.Clases, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(product.TipoProductoBaseCodigo, ProductBaseCodes.MensualidadPorHorario, StringComparison.OrdinalIgnoreCase))
        {
            return MatchesTipoDia(candidate.TipoDia, context.TipoDia)
                && product.BloqueHorarioComercialId.HasValue
                && candidate.BloqueHorarioComercialId == product.BloqueHorarioComercialId;
        }

        if (IsPackTarifaProductCode(product.TipoProductoBaseCodigo))
        {
            return MatchesTipoDia(candidate.TipoDia, context.TipoDia);
        }

        if (!IsCurrentTimeTarifaProductCode(product.TipoProductoBaseCodigo))
        {
            return false;
        }

        return MatchesTipoDia(candidate.TipoDia, context.TipoDia)
            && (!candidate.BloqueHorarioComercialId.HasValue || context.ActiveBloqueIds.Contains(candidate.BloqueHorarioComercialId.Value));
    }

    private static TarifaCandidate? SelectTarifaCandidate(IEnumerable<TarifaCandidate> candidates, PosCatalogProductInfo product, string tipoClienteCode)
    {
        return candidates
            .Where(x => x.TipoClienteCodigo == tipoClienteCode)
            .OrderByDescending(x => ShouldPrioritizeSpecificBloque(product.TipoProductoBaseCodigo) && x.BloqueHorarioComercialId.HasValue)
            .ThenByDescending(x => x.VigenciaDesde)
            .FirstOrDefault();
    }

    private static bool ShouldPrioritizeSpecificBloque(string productCode)
    {
        return IsCurrentTimeTarifaProductCode(productCode)
            || string.Equals(productCode, ProductBaseCodes.MensualidadPorHorario, StringComparison.OrdinalIgnoreCase);
    }

    private static string? BuildBloqueLabel(TarifaCandidate? tarifa, IReadOnlyDictionary<long, BloqueCatalogInfo> bloquesById)
    {
        if (tarifa is null || !tarifa.BloqueHorarioComercialId.HasValue)
        {
            return null;
        }

        if (!bloquesById.TryGetValue(tarifa.BloqueHorarioComercialId.Value, out var bloque))
        {
            return null;
        }

        return $"({bloque.HoraInicio:HH\\:mm}-{bloque.HoraFin:HH\\:mm})";
    }

    private static string ToDayCode(short diaSemana)
    {
        return diaSemana switch
        {
            1 => "LUN",
            2 => "MAR",
            3 => "MIE",
            4 => "JUE",
            5 => "VIE",
            6 => "SAB",
            7 => "DOM",
            _ => $"D{diaSemana}",
        };
    }

    public async Task<VentaPreviewDto> PreviewVentaAsync(PreviewVentaRequestDto request, CancellationToken cancellationToken)
    {
        var empresaId = GetRequiredEmpresaId();

        if (request.Items.Count == 0)
        {
            throw new InvalidOperationException("Debe incluir al menos un item en la venta.");
        }

        var productoIds = request.Items.Select(x => x.ProductoEmpresaId).Distinct().ToArray();
        var productos = await DbContext.ProductosEmpresa
            .Include(x => x.TipoProductoBase)
            .Where(x => x.EmpresaId == empresaId && productoIds.Contains(x.ProductoEmpresaId))
            .ToDictionaryAsync(x => x.ProductoEmpresaId, cancellationToken);

        var assignedClientIds = request.Items
            .Select(item => ResolveAssignedClientId(item, request.ClienteEmpresaId))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();

        var clientesById = assignedClientIds.Length == 0
            ? new Dictionary<long, ClienteEmpresaInfo>()
            : await DbContext.ClientesEmpresa
                .AsNoTracking()
                .Include(x => x.Persona)
                .Include(x => x.TipoCliente)
                .Where(x => x.EmpresaId == empresaId && assignedClientIds.Contains(x.ClienteEmpresaId))
                .Select(x => new ClienteEmpresaInfo(x.ClienteEmpresaId, x.Persona.NombreCompleto, x.TipoClienteId, x.TipoCliente.Nombre, x.Estado))
                .ToDictionaryAsync(x => x.ClienteEmpresaId, cancellationToken);

        var missingClientId = assignedClientIds.FirstOrDefault(id => !clientesById.ContainsKey(id));
        if (missingClientId != 0)
        {
            throw new InvalidOperationException($"Cliente asignado {missingClientId} no encontrado.");
        }

        var detalles = new List<VentaPreviewDetalleDto>();

        foreach (var item in request.Items)
        {
            if (!productos.TryGetValue(item.ProductoEmpresaId, out var producto))
            {
                throw new InvalidOperationException($"Producto {item.ProductoEmpresaId} no encontrado.");
            }

            if (item.Cantidad <= 0)
            {
                throw new InvalidOperationException($"Cantidad inválida para el producto {producto.NombreComercial}.");
            }

            if (IsSingleUnitAssignedProduct(producto) && item.Cantidad != 1)
            {
                throw new InvalidOperationException($"El producto {producto.NombreComercial} debe venderse de a una unidad por línea.");
            }

            var assignedClientId = ResolveAssignedClientId(item, request.ClienteEmpresaId);
            if (RequiresAssignedClient(producto) && !assignedClientId.HasValue)
            {
                throw new InvalidOperationException($"El producto {producto.NombreComercial} requiere cliente asignado.");
            }

            var assignedClient = assignedClientId.HasValue
                ? clientesById.GetValueOrDefault(assignedClientId.Value) ?? throw new InvalidOperationException($"Cliente asignado no encontrado para el producto {producto.NombreComercial}.")
                : null;

            if (assignedClient is not null && !string.Equals(assignedClient.Estado, "activo", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"El cliente asignado al producto {producto.NombreComercial} no está activo.");
            }

            var precio = await ResolvePriceAsync(producto, assignedClient?.TipoClienteId, cancellationToken);
            detalles.Add(new VentaPreviewDetalleDto(producto.ProductoEmpresaId, producto.NombreComercial, item.Cantidad, precio, precio * item.Cantidad));
        }

        await ValidateClasesRulesAsync(empresaId, request, productos, clientesById, cancellationToken);

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

        var productoIds = request.Items.Select(x => x.ProductoEmpresaId).Distinct().ToArray();
        var productos = await DbContext.ProductosEmpresa
            .Include(x => x.TipoProductoBase)
            .Where(x => x.EmpresaId == empresaId && productoIds.Contains(x.ProductoEmpresaId))
            .ToDictionaryAsync(x => x.ProductoEmpresaId, cancellationToken);

        var assignedClientIds = request.Items
            .Select(item => ResolveAssignedClientId(item, request.ClienteEmpresaId))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();

        var clientesById = assignedClientIds.Length == 0
            ? new Dictionary<long, ClienteEmpresaInfo>()
            : await DbContext.ClientesEmpresa
                .AsNoTracking()
                .Include(x => x.Persona)
                .Include(x => x.TipoCliente)
                .Where(x => x.EmpresaId == empresaId && assignedClientIds.Contains(x.ClienteEmpresaId))
                .Select(x => new ClienteEmpresaInfo(x.ClienteEmpresaId, x.Persona.NombreCompleto, x.TipoClienteId, x.TipoCliente.Nombre, x.Estado))
                .ToDictionaryAsync(x => x.ClienteEmpresaId, cancellationToken);

        var missingClientId = assignedClientIds.FirstOrDefault(id => !clientesById.ContainsKey(id));
        if (missingClientId != 0)
        {
            throw new InvalidOperationException($"Cliente asignado {missingClientId} no encontrado.");
        }

        var venta = new Venta
        {
            EmpresaId = empresaId,
            ClienteEmpresaId = request.ClienteEmpresaId,
            UsuarioVendedorId = usuarioId,
            NumeroComprobante = GenerateComprobanteNumber(),
            FechaHora = DateTimeOffset.UtcNow,
            Estado = "emitida"
        };

        var lineItems = new List<(VentaDetalle Detalle, Domain.Entities.Administracion.ProductoEmpresa Producto, long? AssignedClientId)>();

        foreach (var item in request.Items)
        {
            if (!productos.TryGetValue(item.ProductoEmpresaId, out var producto))
            {
                throw new InvalidOperationException($"Producto {item.ProductoEmpresaId} no encontrado.");
            }

            if (item.Cantidad <= 0)
            {
                throw new InvalidOperationException($"Cantidad inválida para el producto {producto.NombreComercial}.");
            }

            if (IsSingleUnitAssignedProduct(producto) && item.Cantidad != 1)
            {
                throw new InvalidOperationException($"El producto {producto.NombreComercial} debe venderse de a una unidad por línea.");
            }

            var assignedClientId = ResolveAssignedClientId(item, request.ClienteEmpresaId);
            if (RequiresAssignedClient(producto) && !assignedClientId.HasValue)
            {
                throw new InvalidOperationException($"El producto {producto.NombreComercial} requiere cliente asignado.");
            }

            var assignedClient = assignedClientId.HasValue
                ? clientesById.GetValueOrDefault(assignedClientId.Value) ?? throw new InvalidOperationException($"Cliente asignado no encontrado para el producto {producto.NombreComercial}.")
                : null;

            if (assignedClient is not null && !string.Equals(assignedClient.Estado, "activo", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"El cliente asignado al producto {producto.NombreComercial} no está activo.");
            }

            var precio = await ResolvePriceAsync(producto, assignedClient?.TipoClienteId, cancellationToken);
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
            lineItems.Add((detalle, producto, assignedClientId));
        }

        await ValidateClasesRulesAsync(empresaId, new PreviewVentaRequestDto(request.ClienteEmpresaId, request.Items), productos, clientesById, cancellationToken);

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

        foreach (var line in lineItems)
        {
            if (!line.Producto.GeneraBeneficio)
            {
                continue;
            }

            if (!line.AssignedClientId.HasValue)
            {
                throw new InvalidOperationException($"El producto {line.Producto.NombreComercial} requiere cliente asignado para generar beneficio.");
            }

            var beneficio = await BuildBenefitAsync(empresaId, line.AssignedClientId.Value, line.Producto, line.Detalle, cancellationToken);
            DbContext.BeneficiosCliente.Add(beneficio);
        }

        await DbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await AuditAsync("venta", venta.VentaId, "crear", new { venta.NumeroComprobante, venta.Total }, empresaId, cancellationToken);

        return await GetVentaAsync(venta.VentaId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<VentaResumenDto>> GetVentasAsync(string? estado, CancellationToken cancellationToken)
    {
        var empresaId = GetRequiredEmpresaId();
        var estadoNormalizado = estado?.Trim().ToLowerInvariant();

        var query = DbContext.Ventas
            .AsNoTracking()
            .Where(x => x.EmpresaId == empresaId);

        if (estadoNormalizado == "emitida" || estadoNormalizado == "anulada")
        {
            query = query.Where(x => x.Estado == estadoNormalizado);
        }

        return await query
            .OrderByDescending(x => x.FechaHora)
            .Take(100)
            .Select(x => new VentaResumenDto(
                x.VentaId,
                x.NumeroComprobante,
                x.FechaHora,
                x.Estado,
                x.Total,
                x.ClienteEmpresa != null ? x.ClienteEmpresa.Persona.NombreCompleto : null,
                x.MotivoAnulacion))
            .ToListAsync(cancellationToken);
    }

    public async Task<VentaDto> GetVentaAsync(long ventaId, CancellationToken cancellationToken)
    {
        var empresaId = GetRequiredEmpresaId();
        var venta = await GetVentaEntityForDetailAsync(empresaId, ventaId, cancellationToken);
        return MapVenta(venta);
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

    private async Task<Venta> GetVentaEntityForDetailAsync(long empresaId, long ventaId, CancellationToken cancellationToken)
    {
        return await DbContext.Ventas
            .AsNoTracking()
            .Include(x => x.ClienteEmpresa).ThenInclude(x => x!.Persona)
            .Include(x => x.Detalles).ThenInclude(x => x.BeneficioCliente)
            .Include(x => x.Pagos).ThenInclude(x => x.MedioPago)
            .FirstAsync(x => x.EmpresaId == empresaId && x.VentaId == ventaId, cancellationToken);
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

        var empresaId = GetRequiredEmpresaId();
        var context = await BuildTarifaContextAsync(empresaId, cancellationToken);
        var productCode = producto.TipoProductoBase.Codigo;

        var tarifas = await DbContext.TarifasProducto
            .Where(x => x.ProductoEmpresaId == producto.ProductoEmpresaId
                && x.Activo
                && x.VigenciaDesde <= context.Today
                && context.Today <= x.VigenciaHasta
                && x.TipoClienteId == tipoClienteId)
            .ToListAsync(cancellationToken);

        IEnumerable<Domain.Entities.Administracion.TarifaProducto> tarifasFiltradas;

        if (string.Equals(productCode, ProductBaseCodes.Clases, StringComparison.OrdinalIgnoreCase))
        {
            tarifasFiltradas = tarifas;
        }
        else if (string.Equals(productCode, ProductBaseCodes.MensualidadPorHorario, StringComparison.OrdinalIgnoreCase))
        {
            tarifasFiltradas = tarifas.Where(x =>
                MatchesTipoDia(x.TipoDia, context.TipoDia)
                && producto.BloqueHorarioComercialId.HasValue
                && x.BloqueHorarioComercialId == producto.BloqueHorarioComercialId);
        }
        else if (IsPackTarifaProductCode(productCode))
        {
            tarifasFiltradas = tarifas.Where(x => MatchesTipoDia(x.TipoDia, context.TipoDia));
        }
        else if (IsCurrentTimeTarifaProductCode(productCode))
        {
            tarifasFiltradas = tarifas.Where(x =>
                MatchesTipoDia(x.TipoDia, context.TipoDia)
                && (!x.BloqueHorarioComercialId.HasValue || context.ActiveBloqueIds.Contains(x.BloqueHorarioComercialId.Value)));
        }
        else
        {
            tarifasFiltradas = tarifas.Where(x =>
                MatchesTipoDia(x.TipoDia, context.TipoDia)
                && x.BloqueHorarioComercialId == producto.BloqueHorarioComercialId);
        }

        var tarifa = tarifasFiltradas
            .OrderByDescending(x => ShouldPrioritizeSpecificBloque(productCode) && x.BloqueHorarioComercialId.HasValue)
            .ThenByDescending(x => x.VigenciaDesde)
            .FirstOrDefault();

        if (tarifa is null)
        {
            throw new InvalidOperationException($"No existe una tarifa vigente para el producto {producto.NombreComercial} (tipoDia={context.TipoDia}, tipoClienteId={(tipoClienteId.HasValue ? tipoClienteId.Value : null)}).");
        }

        return tarifa.Precio;
    }

    private static bool MatchesTipoDia(string? tipoDiaCsv, string tipoDia)
    {
        if (string.IsNullOrWhiteSpace(tipoDiaCsv))
        {
            return false;
        }

        var values = tipoDiaCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToUpperInvariant());

        return values.Any(value => value == tipoDia || (value == "DOM_FEST" && tipoDia == "DOM"));
    }

    private async Task<BeneficioCliente> BuildBenefitAsync(long empresaId, long clienteEmpresaId, Domain.Entities.Administracion.ProductoEmpresa producto, VentaDetalle detalle, CancellationToken cancellationToken)
    {
        var fechaInicio = detalle.FechaInicioVigencia ?? GetChileTimeContext().TodayLocal;
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

    private async Task ValidateClasesRulesAsync(
        long empresaId,
        PreviewVentaRequestDto request,
        IReadOnlyDictionary<long, Domain.Entities.Administracion.ProductoEmpresa> productos,
        IReadOnlyDictionary<long, ClienteEmpresaInfo> clientesById,
        CancellationToken cancellationToken)
    {
        var classAssignments = request.Items
            .Select(item => new
            {
                Item = item,
                Producto = productos[item.ProductoEmpresaId],
                ClienteId = ResolveAssignedClientId(item, request.ClienteEmpresaId)
            })
            .Where(x => x.Producto.TipoProductoBase.Codigo == ProductBaseCodes.Clases)
            .ToList();

        if (classAssignments.Count == 0)
        {
            return;
        }

        var duplicateClientGroup = classAssignments
            .Where(x => x.ClienteId.HasValue)
            .GroupBy(x => x.ClienteId!.Value)
            .FirstOrDefault(x => x.Count() > 1);

        if (duplicateClientGroup is not null)
        {
            var cliente = clientesById.GetValueOrDefault(duplicateClientGroup.Key);
            throw new InvalidOperationException($"El cliente {cliente?.NombreCompleto ?? duplicateClientGroup.Key.ToString()} ya tiene una mensualidad de clases en esta venta.");
        }

        var clienteIds = classAssignments
            .Where(x => x.ClienteId.HasValue)
            .Select(x => x.ClienteId!.Value)
            .Distinct()
            .ToArray();

        if (clienteIds.Length == 0)
        {
            return;
        }

        var claseTipoProductoBaseId = classAssignments[0].Producto.TipoProductoBaseId;
        var today = GetChileTimeContext().TodayLocal;

        var clientesConClaseVigente = await DbContext.BeneficiosCliente
            .AsNoTracking()
            .Where(x => x.EmpresaId == empresaId
                && clienteIds.Contains(x.ClienteEmpresaId)
                && x.TipoProductoBaseId == claseTipoProductoBaseId
                && x.Estado == "vigente"
                && x.FechaInicio <= today
                && today <= x.FechaTermino)
            .Select(x => x.ClienteEmpresaId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (clientesConClaseVigente.Count > 0)
        {
            var nombres = clientesConClaseVigente
                .Select(id => clientesById.GetValueOrDefault(id)?.NombreCompleto ?? id.ToString())
                .ToArray();

            throw new InvalidOperationException($"Los siguientes clientes ya tienen clases activas: {string.Join(", ", nombres)}.");
        }
    }

    private static bool RequiresAssignedClient(Domain.Entities.Administracion.ProductoEmpresa producto)
    {
        var tipoCodigo = producto.TipoProductoBase.Codigo;
        return tipoCodigo is ProductBaseCodes.Clases
            or ProductBaseCodes.MensualidadPorHorario
            or ProductBaseCodes.MensualidadTodoHorario
            or ProductBaseCodes.TicketIndividual
            || ProductBaseCodes.IsPackTickets(tipoCodigo);
    }

    private static bool IsSingleUnitAssignedProduct(Domain.Entities.Administracion.ProductoEmpresa producto) => RequiresAssignedClient(producto);

    private static long? ResolveAssignedClientId(VentaItemRequestDto item, long? fallbackClienteEmpresaId) => item.ClienteEmpresaIdAsignado ?? fallbackClienteEmpresaId;

    private sealed record ClienteEmpresaInfo(long ClienteEmpresaId, string NombreCompleto, long TipoClienteId, string TipoClienteNombre, string Estado);
}
