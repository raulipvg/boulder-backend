namespace ErpBoulder.Domain.Entities.Ventas;

using ErpBoulder.Domain.Entities.Administracion;

public sealed class Venta
{
    public long VentaId { get; set; }
    public long EmpresaId { get; set; }
    public long? ClienteEmpresaId { get; set; }
    public long UsuarioVendedorId { get; set; }
    public string NumeroComprobante { get; set; } = string.Empty;
    public DateTimeOffset FechaHora { get; set; }
    public string Estado { get; set; } = "emitida";
    public decimal Subtotal { get; set; }
    public decimal Descuento { get; set; }
    public decimal Total { get; set; }
    public string? MotivoAnulacion { get; set; }
    public long? AnuladaPorUsuarioId { get; set; }
    public DateTimeOffset? AnuladaAt { get; set; }

    public ClienteEmpresa? ClienteEmpresa { get; set; }
    public Usuario UsuarioVendedor { get; set; } = null!;
    public ICollection<VentaDetalle> Detalles { get; set; } = new List<VentaDetalle>();
    public ICollection<VentaPago> Pagos { get; set; } = new List<VentaPago>();
}

public sealed class VentaDetalle
{
    public long VentaDetalleId { get; set; }
    public long VentaId { get; set; }
    public long ProductoEmpresaId { get; set; }
    public long? TarifaProductoId { get; set; }
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal { get; set; }
    public DateOnly? FechaInicioVigencia { get; set; }
    public string ProductoNombreSnapshot { get; set; } = string.Empty;
    public string? Observacion { get; set; }

    public Venta Venta { get; set; } = null!;
    public ProductoEmpresa ProductoEmpresa { get; set; } = null!;
    public TarifaProducto? TarifaProducto { get; set; }
    public BeneficioCliente? BeneficioCliente { get; set; }
}

public sealed class VentaPago
{
    public long VentaPagoId { get; set; }
    public long VentaId { get; set; }
    public long MedioPagoId { get; set; }
    public decimal Monto { get; set; }
    public string? Referencia { get; set; }

    public MedioPago MedioPago { get; set; } = null!;
}

public sealed class BeneficioCliente
{
    public long BeneficioClienteId { get; set; }
    public long VentaDetalleId { get; set; }
    public long EmpresaId { get; set; }
    public long ClienteEmpresaId { get; set; }
    public long ProductoEmpresaId { get; set; }
    public long TipoProductoBaseId { get; set; }
    public string Estado { get; set; } = "vigente";
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaTermino { get; set; }
    public int? UsosTotales { get; set; }
    public int UsosConsumidos { get; set; }
    public bool AccesoIlimitado { get; set; }
    public long? BloqueHorarioComercialId { get; set; }
    public long? ClaseId { get; set; }
    public long? ProfesorEmpresaId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
