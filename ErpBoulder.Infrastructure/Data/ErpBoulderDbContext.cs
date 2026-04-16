namespace ErpBoulder.Infrastructure.Data;

using ErpBoulder.Domain.Entities.Administracion;
using ErpBoulder.Domain.Entities.Operacion;
using ErpBoulder.Domain.Entities.Ventas;
using Microsoft.EntityFrameworkCore;

public sealed class ErpBoulderDbContext(DbContextOptions<ErpBoulderDbContext> options) : DbContext(options)
{
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Persona> Personas => Set<Persona>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<UsuarioRol> UsuarioRoles => Set<UsuarioRol>();
    public DbSet<TipoCliente> TiposCliente => Set<TipoCliente>();
    public DbSet<ClienteEmpresa> ClientesEmpresa => Set<ClienteEmpresa>();
    public DbSet<ProfesorEmpresa> ProfesoresEmpresa => Set<ProfesorEmpresa>();
    public DbSet<Clase> Clases => Set<Clase>();
    public DbSet<ClaseHorario> ClaseHorarios => Set<ClaseHorario>();
    public DbSet<BloqueHorarioComercial> BloquesHorariosComerciales => Set<BloqueHorarioComercial>();
    public DbSet<Feriado> Feriados => Set<Feriado>();
    public DbSet<TipoProductoBase> TiposProductoBase => Set<TipoProductoBase>();
    public DbSet<ProductoEmpresa> ProductosEmpresa => Set<ProductoEmpresa>();
    public DbSet<TarifaProducto> TarifasProducto => Set<TarifaProducto>();
    public DbSet<MedioPago> MediosPago => Set<MedioPago>();

    public DbSet<Venta> Ventas => Set<Venta>();
    public DbSet<VentaDetalle> VentaDetalles => Set<VentaDetalle>();
    public DbSet<VentaPago> VentaPagos => Set<VentaPago>();
    public DbSet<BeneficioCliente> BeneficiosCliente => Set<BeneficioCliente>();

    public DbSet<ClaseSesion> ClaseSesiones => Set<ClaseSesion>();
    public DbSet<AccesoEvento> AccesoEventos => Set<AccesoEvento>();
    public DbSet<ClaseAsistencia> ClaseAsistencias => Set<ClaseAsistencia>();
    public DbSet<AuditoriaEvento> AuditoriaEventos => Set<AuditoriaEvento>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureAdministracion(modelBuilder);
        ConfigureVentas(modelBuilder);
        ConfigureOperacion(modelBuilder);
    }

    private static void ConfigureAdministracion(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Empresa>(entity =>
        {
            entity.ToTable("empresa", "administracion");
            entity.HasKey(x => x.EmpresaId);
            entity.Property(x => x.EmpresaId).HasColumnName("empresa_id");
            entity.Property(x => x.NombreComercial).HasColumnName("nombre_comercial");
            entity.Property(x => x.RazonSocial).HasColumnName("razon_social");
            entity.Property(x => x.Rut).HasColumnName("rut");
            entity.Property(x => x.Estado).HasColumnName("estado");
            entity.Property(x => x.MonedaCodigo).HasColumnName("moneda_codigo");
            entity.Property(x => x.TelefonoContacto).HasColumnName("telefono_contacto");
            entity.Property(x => x.CorreoContacto).HasColumnName("correo_contacto");
            entity.Property(x => x.Timezone).HasColumnName("timezone");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<Persona>(entity =>
        {
            entity.ToTable("persona", "administracion");
            entity.HasKey(x => x.PersonaId);
            entity.Property(x => x.PersonaId).HasColumnName("persona_id");
            entity.Property(x => x.Rut).HasColumnName("rut");
            entity.Property(x => x.NombreCompleto).HasColumnName("nombre_completo");
            entity.Property(x => x.FechaNacimiento).HasColumnName("fecha_nacimiento");
            entity.Property(x => x.Telefono).HasColumnName("telefono");
            entity.Property(x => x.Correo).HasColumnName("correo");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.ToTable("usuario", "administracion");
            entity.HasKey(x => x.UsuarioId);
            entity.Property(x => x.UsuarioId).HasColumnName("usuario_id");
            entity.Property(x => x.PersonaId).HasColumnName("persona_id");
            entity.Property(x => x.EmailLogin).HasColumnName("email_login");
            entity.Property(x => x.PasswordHash).HasColumnName("password_hash");
            entity.Property(x => x.Estado).HasColumnName("estado");
            entity.Property(x => x.UltimoAccesoAt).HasColumnName("ultimo_acceso_at");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.HasOne(x => x.Persona).WithMany().HasForeignKey(x => x.PersonaId);
        });

        modelBuilder.Entity<Rol>(entity =>
        {
            entity.ToTable("rol", "administracion");
            entity.HasKey(x => x.RolId);
            entity.Property(x => x.RolId).HasColumnName("rol_id");
            entity.Property(x => x.Codigo).HasColumnName("codigo");
            entity.Property(x => x.Nombre).HasColumnName("nombre");
        });

        modelBuilder.Entity<UsuarioRol>(entity =>
        {
            entity.ToTable("usuario_rol", "administracion");
            entity.HasKey(x => x.UsuarioRolId);
            entity.Property(x => x.UsuarioRolId).HasColumnName("usuario_rol_id");
            entity.Property(x => x.UsuarioId).HasColumnName("usuario_id");
            entity.Property(x => x.RolId).HasColumnName("rol_id");
            entity.Property(x => x.EmpresaId).HasColumnName("empresa_id");
            entity.Property(x => x.Activo).HasColumnName("activo");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.HasOne(x => x.Usuario).WithMany(x => x.Roles).HasForeignKey(x => x.UsuarioId);
            entity.HasOne(x => x.Rol).WithMany().HasForeignKey(x => x.RolId);
            entity.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId);
        });

        modelBuilder.Entity<TipoCliente>(entity =>
        {
            entity.ToTable("tipo_cliente", "administracion");
            entity.HasKey(x => x.TipoClienteId);
            entity.Property(x => x.TipoClienteId).HasColumnName("tipo_cliente_id");
            entity.Property(x => x.EmpresaId).HasColumnName("empresa_id");
            entity.Property(x => x.Codigo).HasColumnName("codigo");
            entity.Property(x => x.Nombre).HasColumnName("nombre");
            entity.Property(x => x.Activo).HasColumnName("activo");
        });

        modelBuilder.Entity<ClienteEmpresa>(entity =>
        {
            entity.ToTable("cliente_empresa", "administracion");
            entity.HasKey(x => x.ClienteEmpresaId);
            entity.Property(x => x.ClienteEmpresaId).HasColumnName("cliente_empresa_id");
            entity.Property(x => x.EmpresaId).HasColumnName("empresa_id");
            entity.Property(x => x.PersonaId).HasColumnName("persona_id");
            entity.Property(x => x.TipoClienteId).HasColumnName("tipo_cliente_id");
            entity.Property(x => x.Estado).HasColumnName("estado");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.HasOne(x => x.Empresa).WithMany().HasForeignKey(x => x.EmpresaId);
            entity.HasOne(x => x.Persona).WithMany().HasForeignKey(x => x.PersonaId);
            entity.HasOne(x => x.TipoCliente).WithMany().HasForeignKey(x => x.TipoClienteId);
        });

        modelBuilder.Entity<ProfesorEmpresa>(entity =>
        {
            entity.ToTable("profesor_empresa", "administracion");
            entity.HasKey(x => x.ProfesorEmpresaId);
            entity.Property(x => x.ProfesorEmpresaId).HasColumnName("profesor_empresa_id");
            entity.Property(x => x.EmpresaId).HasColumnName("empresa_id");
            entity.Property(x => x.PersonaId).HasColumnName("persona_id");
            entity.Property(x => x.Especialidad).HasColumnName("especialidad");
            entity.Property(x => x.Estado).HasColumnName("estado");
            entity.HasOne(x => x.Persona).WithMany().HasForeignKey(x => x.PersonaId);
        });

        modelBuilder.Entity<Clase>(entity =>
        {
            entity.ToTable("clase", "administracion");
            entity.HasKey(x => x.ClaseId);
            entity.Property(x => x.ClaseId).HasColumnName("clase_id");
            entity.Property(x => x.EmpresaId).HasColumnName("empresa_id");
            entity.Property(x => x.Nombre).HasColumnName("nombre");
            entity.Property(x => x.ProfesorEmpresaId).HasColumnName("profesor_empresa_id");
            entity.Property(x => x.CupoMaximo).HasColumnName("cupo_maximo");
            entity.Property(x => x.Estado).HasColumnName("estado");
            entity.HasOne(x => x.ProfesorEmpresa).WithMany().HasForeignKey(x => x.ProfesorEmpresaId);
            entity.HasMany(x => x.Horarios).WithOne().HasForeignKey(x => x.ClaseId);
        });

        modelBuilder.Entity<ClaseHorario>(entity =>
        {
            entity.ToTable("clase_horario", "administracion");
            entity.HasKey(x => x.ClaseHorarioId);
            entity.Property(x => x.ClaseHorarioId).HasColumnName("clase_horario_id");
            entity.Property(x => x.ClaseId).HasColumnName("clase_id");
            entity.Property(x => x.DiaSemana).HasColumnName("dia_semana");
            entity.Property(x => x.HoraInicio).HasColumnName("hora_inicio");
            entity.Property(x => x.HoraFin).HasColumnName("hora_fin");
            entity.Property(x => x.Activo).HasColumnName("activo");
        });

        modelBuilder.Entity<BloqueHorarioComercial>(entity =>
        {
            entity.ToTable("bloque_horario_comercial", "administracion");
            entity.HasKey(x => x.BloqueHorarioComercialId);
            entity.Property(x => x.BloqueHorarioComercialId).HasColumnName("bloque_horario_comercial_id");
            entity.Property(x => x.EmpresaId).HasColumnName("empresa_id");
            entity.Property(x => x.Nombre).HasColumnName("nombre");
            entity.Property(x => x.HoraInicio).HasColumnName("hora_inicio");
            entity.Property(x => x.HoraFin).HasColumnName("hora_fin");
            entity.Property(x => x.Activo).HasColumnName("activo");
        });

        modelBuilder.Entity<Feriado>(entity =>
        {
            entity.ToTable("feriado", "administracion");
            entity.HasKey(x => x.Fecha);
            entity.Property(x => x.Fecha).HasColumnName("fecha");
            entity.Property(x => x.Nombre).HasColumnName("nombre");
        });

        modelBuilder.Entity<TipoProductoBase>(entity =>
        {
            entity.ToTable("tipo_producto_base", "administracion");
            entity.HasKey(x => x.TipoProductoBaseId);
            entity.Property(x => x.TipoProductoBaseId).HasColumnName("tipo_producto_base_id");
            entity.Property(x => x.Codigo).HasColumnName("codigo");
            entity.Property(x => x.Nombre).HasColumnName("nombre");
        });

        modelBuilder.Entity<ProductoEmpresa>(entity =>
        {
            entity.ToTable("producto_empresa", "administracion");
            entity.HasKey(x => x.ProductoEmpresaId);
            entity.Property(x => x.ProductoEmpresaId).HasColumnName("producto_empresa_id");
            entity.Property(x => x.EmpresaId).HasColumnName("empresa_id");
            entity.Property(x => x.TipoProductoBaseId).HasColumnName("tipo_producto_base_id");
            entity.Property(x => x.NombreComercial).HasColumnName("nombre_comercial");
            entity.Property(x => x.Descripcion).HasColumnName("descripcion");
            entity.Property(x => x.ModoPrecio).HasColumnName("modo_precio");
            entity.Property(x => x.PrecioFijo).HasColumnName("precio_fijo");
            entity.Property(x => x.VisiblePos).HasColumnName("visible_pos");
            entity.Property(x => x.Activo).HasColumnName("activo");
            entity.Property(x => x.TarifaAsociada).HasColumnName("tarifa_asociada");
            entity.Property(x => x.RequiereCliente).HasColumnName("requiere_cliente");
            entity.Property(x => x.GeneraBeneficio).HasColumnName("genera_beneficio");
            entity.Property(x => x.BloqueHorarioComercialId).HasColumnName("bloque_horario_comercial_id");
            entity.Property(x => x.ClaseId).HasColumnName("clase_id");
            entity.Property(x => x.VigenciaDias).HasColumnName("vigencia_dias");
            entity.Property(x => x.UsosIncluidos).HasColumnName("usos_incluidos");
            entity.Property(x => x.AccesoIlimitado).HasColumnName("acceso_ilimitado");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.HasOne(x => x.TipoProductoBase).WithMany().HasForeignKey(x => x.TipoProductoBaseId);
            entity.HasOne(x => x.BloqueHorarioComercial).WithMany().HasForeignKey(x => x.BloqueHorarioComercialId);
            entity.HasOne(x => x.Clase).WithMany().HasForeignKey(x => x.ClaseId);
        });

        modelBuilder.Entity<TarifaProducto>(entity =>
        {
            entity.ToTable("tarifa_producto", "administracion");
            entity.HasKey(x => x.TarifaProductoId);
            entity.Property(x => x.TarifaProductoId).HasColumnName("tarifa_producto_id");
            entity.Property(x => x.ProductoEmpresaId).HasColumnName("producto_empresa_id");
            entity.Property(x => x.TipoClienteId).HasColumnName("tipo_cliente_id");
            entity.Property(x => x.TipoDia).HasColumnName("tipo_dia");
            entity.Property(x => x.BloqueHorarioComercialId).HasColumnName("bloque_horario_comercial_id");
            entity.Property(x => x.Precio).HasColumnName("precio");
            entity.Property(x => x.VigenciaDesde).HasColumnName("vigencia_desde");
            entity.Property(x => x.VigenciaHasta).HasColumnName("vigencia_hasta");
            entity.Property(x => x.Activo).HasColumnName("activo");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<MedioPago>(entity =>
        {
            entity.ToTable("medio_pago", "administracion");
            entity.HasKey(x => x.MedioPagoId);
            entity.Property(x => x.MedioPagoId).HasColumnName("medio_pago_id");
            entity.Property(x => x.Codigo).HasColumnName("codigo");
            entity.Property(x => x.Nombre).HasColumnName("nombre");
            entity.Property(x => x.Activo).HasColumnName("activo");
        });
    }

    private static void ConfigureVentas(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Venta>(entity =>
        {
            entity.ToTable("venta", "ventas");
            entity.HasKey(x => x.VentaId);
            entity.Property(x => x.VentaId).HasColumnName("venta_id");
            entity.Property(x => x.EmpresaId).HasColumnName("empresa_id");
            entity.Property(x => x.ClienteEmpresaId).HasColumnName("cliente_empresa_id");
            entity.Property(x => x.UsuarioVendedorId).HasColumnName("usuario_vendedor_id");
            entity.Property(x => x.NumeroComprobante).HasColumnName("numero_comprobante");
            entity.Property(x => x.FechaHora).HasColumnName("fecha_hora");
            entity.Property(x => x.Estado).HasColumnName("estado");
            entity.Property(x => x.Subtotal).HasColumnName("subtotal");
            entity.Property(x => x.Descuento).HasColumnName("descuento");
            entity.Property(x => x.Total).HasColumnName("total");
            entity.Property(x => x.MotivoAnulacion).HasColumnName("motivo_anulacion");
            entity.Property(x => x.AnuladaPorUsuarioId).HasColumnName("anulada_por_usuario_id");
            entity.Property(x => x.AnuladaAt).HasColumnName("anulada_at");
            entity.HasOne(x => x.ClienteEmpresa).WithMany().HasForeignKey(x => x.ClienteEmpresaId);
            entity.HasOne(x => x.UsuarioVendedor).WithMany().HasForeignKey(x => x.UsuarioVendedorId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(x => x.Detalles).WithOne(x => x.Venta).HasForeignKey(x => x.VentaId);
            entity.HasMany(x => x.Pagos).WithOne().HasForeignKey(x => x.VentaId);
        });

        modelBuilder.Entity<VentaDetalle>(entity =>
        {
            entity.ToTable("venta_detalle", "ventas");
            entity.HasKey(x => x.VentaDetalleId);
            entity.Property(x => x.VentaDetalleId).HasColumnName("venta_detalle_id");
            entity.Property(x => x.VentaId).HasColumnName("venta_id");
            entity.Property(x => x.ProductoEmpresaId).HasColumnName("producto_empresa_id");
            entity.Property(x => x.TarifaProductoId).HasColumnName("tarifa_producto_id");
            entity.Property(x => x.Cantidad).HasColumnName("cantidad");
            entity.Property(x => x.PrecioUnitario).HasColumnName("precio_unitario");
            entity.Property(x => x.Subtotal).HasColumnName("subtotal");
            entity.Property(x => x.FechaInicioVigencia).HasColumnName("fecha_inicio_vigencia");
            entity.Property(x => x.ProductoNombreSnapshot).HasColumnName("producto_nombre_snapshot");
            entity.Property(x => x.Observacion).HasColumnName("observacion");
            entity.HasOne(x => x.ProductoEmpresa).WithMany().HasForeignKey(x => x.ProductoEmpresaId);
            entity.HasOne(x => x.TarifaProducto).WithMany().HasForeignKey(x => x.TarifaProductoId);
            entity.HasOne(x => x.BeneficioCliente).WithOne().HasForeignKey<BeneficioCliente>(x => x.VentaDetalleId);
        });

        modelBuilder.Entity<VentaPago>(entity =>
        {
            entity.ToTable("venta_pago", "ventas");
            entity.HasKey(x => x.VentaPagoId);
            entity.Property(x => x.VentaPagoId).HasColumnName("venta_pago_id");
            entity.Property(x => x.VentaId).HasColumnName("venta_id");
            entity.Property(x => x.MedioPagoId).HasColumnName("medio_pago_id");
            entity.Property(x => x.Monto).HasColumnName("monto");
            entity.Property(x => x.Referencia).HasColumnName("referencia");
            entity.HasOne(x => x.MedioPago).WithMany().HasForeignKey(x => x.MedioPagoId);
        });

        modelBuilder.Entity<BeneficioCliente>(entity =>
        {
            entity.ToTable("beneficio_cliente", "ventas");
            entity.HasKey(x => x.BeneficioClienteId);
            entity.Property(x => x.BeneficioClienteId).HasColumnName("beneficio_cliente_id");
            entity.Property(x => x.VentaDetalleId).HasColumnName("venta_detalle_id");
            entity.Property(x => x.EmpresaId).HasColumnName("empresa_id");
            entity.Property(x => x.ClienteEmpresaId).HasColumnName("cliente_empresa_id");
            entity.Property(x => x.ProductoEmpresaId).HasColumnName("producto_empresa_id");
            entity.Property(x => x.TipoProductoBaseId).HasColumnName("tipo_producto_base_id");
            entity.Property(x => x.Estado).HasColumnName("estado");
            entity.Property(x => x.FechaInicio).HasColumnName("fecha_inicio");
            entity.Property(x => x.FechaTermino).HasColumnName("fecha_termino");
            entity.Property(x => x.UsosTotales).HasColumnName("usos_totales");
            entity.Property(x => x.UsosConsumidos).HasColumnName("usos_consumidos");
            entity.Property(x => x.AccesoIlimitado).HasColumnName("acceso_ilimitado");
            entity.Property(x => x.BloqueHorarioComercialId).HasColumnName("bloque_horario_comercial_id");
            entity.Property(x => x.ClaseId).HasColumnName("clase_id");
            entity.Property(x => x.ProfesorEmpresaId).HasColumnName("profesor_empresa_id");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });
    }

    private static void ConfigureOperacion(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClaseSesion>(entity =>
        {
            entity.ToTable("clase_sesion", "operacion");
            entity.HasKey(x => x.ClaseSesionId);
            entity.Property(x => x.ClaseSesionId).HasColumnName("clase_sesion_id");
            entity.Property(x => x.EmpresaId).HasColumnName("empresa_id");
            entity.Property(x => x.ClaseId).HasColumnName("clase_id");
            entity.Property(x => x.Fecha).HasColumnName("fecha");
            entity.Property(x => x.HoraInicio).HasColumnName("hora_inicio");
            entity.Property(x => x.HoraFin).HasColumnName("hora_fin");
            entity.Property(x => x.ProfesorEmpresaId).HasColumnName("profesor_empresa_id");
            entity.Property(x => x.CupoMaximo).HasColumnName("cupo_maximo");
            entity.Property(x => x.Estado).HasColumnName("estado");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<AccesoEvento>(entity =>
        {
            entity.ToTable("acceso_evento", "operacion");
            entity.HasKey(x => x.AccesoEventoId);
            entity.Property(x => x.AccesoEventoId).HasColumnName("acceso_evento_id");
            entity.Property(x => x.EmpresaId).HasColumnName("empresa_id");
            entity.Property(x => x.ClienteEmpresaId).HasColumnName("cliente_empresa_id");
            entity.Property(x => x.BeneficioClienteId).HasColumnName("beneficio_cliente_id");
            entity.Property(x => x.ProductoEmpresaId).HasColumnName("producto_empresa_id");
            entity.Property(x => x.UsuarioValidadorId).HasColumnName("usuario_validador_id");
            entity.Property(x => x.FechaHora).HasColumnName("fecha_hora");
            entity.Property(x => x.Resultado).HasColumnName("resultado");
            entity.Property(x => x.MotivoRechazo).HasColumnName("motivo_rechazo");
        });

        modelBuilder.Entity<ClaseAsistencia>(entity =>
        {
            entity.ToTable("clase_asistencia", "operacion");
            entity.HasKey(x => x.ClaseAsistenciaId);
            entity.Property(x => x.ClaseAsistenciaId).HasColumnName("clase_asistencia_id");
            entity.Property(x => x.ClaseSesionId).HasColumnName("clase_sesion_id");
            entity.Property(x => x.ClienteEmpresaId).HasColumnName("cliente_empresa_id");
            entity.Property(x => x.BeneficioClienteId).HasColumnName("beneficio_cliente_id");
            entity.Property(x => x.UsuarioRegistroId).HasColumnName("usuario_registro_id");
            entity.Property(x => x.FechaHoraRegistro).HasColumnName("fecha_hora_registro");
            entity.Property(x => x.Estado).HasColumnName("estado");
        });

        modelBuilder.Entity<AuditoriaEvento>(entity =>
        {
            entity.ToTable("auditoria_evento", "operacion");
            entity.HasKey(x => x.AuditoriaEventoId);
            entity.Property(x => x.AuditoriaEventoId).HasColumnName("auditoria_evento_id");
            entity.Property(x => x.EmpresaId).HasColumnName("empresa_id");
            entity.Property(x => x.UsuarioId).HasColumnName("usuario_id");
            entity.Property(x => x.Entidad).HasColumnName("entidad");
            entity.Property(x => x.EntidadId).HasColumnName("entidad_id");
            entity.Property(x => x.Accion).HasColumnName("accion");
            entity.Property(x => x.FechaHora).HasColumnName("fecha_hora");
            entity.Property(x => x.DetalleJson).HasColumnName("detalle_json").HasColumnType("jsonb");
        });
    }
}
