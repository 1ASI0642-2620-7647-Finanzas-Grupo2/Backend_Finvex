namespace Finvex.Domain;

public sealed class Tienda
{
    public long Id { get; set; }
    public string Ruc { get; set; } = string.Empty;
    public string RazonSocial { get; set; } = string.Empty;
    public string Giro { get; set; } = string.Empty;
    public string Usuario { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
    public ICollection<Cliente> Clientes { get; set; } = new List<Cliente>();
}

public sealed class Cliente
{
    public long Id { get; set; }
    public long TiendaId { get; set; }
    public string Dni { get; set; } = string.Empty;
    public string NombresCompletos { get; set; } = string.Empty;
    public string Usuario { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public decimal LimiteCredito { get; set; }
    public TipoTasa TipoTasa { get; set; }
    public decimal TasaCompensatoria { get; set; }
    public decimal TasaMoratoria { get; set; }
    public int DiaCorte { get; set; }
    public int DiaPago { get; set; }
    public bool Activo { get; set; } = true;
    public Moneda Moneda { get; set; } = Moneda.PEN;
    public int MaxMeses { get; set; } = 1;
    public TimeSpan HoraCorte { get; set; } = new(23, 59, 59);
    public ICollection<Compra> Compras { get; set; } = new List<Compra>();
    public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
}

public sealed class Compra
{
    public long Id { get; set; }
    public long ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
    public string Producto { get; set; } = string.Empty;
    public decimal PrecioCredito { get; set; }
    public decimal SaldoCapital { get; set; }
    public ModalidadCompra Modalidad { get; set; }
    public int PlazoMeses { get; set; }
    public DateTime FechaCompra { get; set; }
    public long? ProductoId { get; set; }
    public int Cantidad { get; set; } = 1;
    public EstadoCompra Estado { get; set; } = EstadoCompra.Pendiente;
    public ICollection<Cronograma> Cronogramas { get; set; } = new List<Cronograma>();
}

public sealed class Cronograma
{
    public long Id { get; set; }
    public long CompraId { get; set; }
    public Compra? Compra { get; set; }
    public int NroCuota { get; set; }
    public DateTime FechaVencimiento { get; set; }
    public decimal SaldoInicial { get; set; }
    public decimal Interes { get; set; }
    public decimal Amortizacion { get; set; }
    public decimal CuotaFija { get; set; }
    public EstadoCronograma Estado { get; set; } = EstadoCronograma.Pendiente;
}

public sealed class Pago
{
    public long Id { get; set; }
    public long ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
    public decimal MontoAbonado { get; set; }
    public DateTime FechaPago { get; set; }
    public decimal ImputacionMora { get; set; }
    public decimal ImputacionInteres { get; set; }
    public decimal ImputacionCapital { get; set; }
}

public sealed class AdministradorSistema
{
    public long Id { get; set; }
    public string Usuario { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
}

public sealed class Producto
{
    public long Id { get; set; }
    public long TiendaId { get; set; }
    public string? Proveedor { get; set; }
    public string Marca { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = string.Empty;
    public string? ImagenUrl { get; set; }
    public decimal PrecioContado { get; set; }
    public decimal PrecioLista { get; set; }
    public bool PermiteFinDeMes { get; set; } = true;
    public bool PermiteCuotas { get; set; } = true;
    public bool Activo { get; set; } = true;
}

public sealed class ListadoPago
{
    public long Id { get; set; }
    public long ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
    public DateTime FechaCorte { get; set; }
    public DateTime FechaPago { get; set; }
    public DateTime FechaCalculo { get; set; }
    public decimal Total { get; set; }
    public DateTime FechaGeneracionUtc { get; set; }
    public ICollection<ItemListadoPago> Items { get; set; } = new List<ItemListadoPago>();
}

public sealed class ItemListadoPago
{
    public long Id { get; set; }
    public long ListadoPagoId { get; set; }
    public ListadoPago? ListadoPago { get; set; }
    public int Orden { get; set; }
    public TipoItemListado Tipo { get; set; }
    public long? CompraId { get; set; }
    public int? NroCuota { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public decimal Capital { get; set; }
    public int Dias { get; set; }
    public decimal InteresCompensatorio { get; set; }
    public decimal Monto { get; set; }
}

public sealed class Operacion
{
    public long Id { get; set; }
    public long? TiendaId { get; set; }
    public string ActorRol { get; set; } = string.Empty;
    public long? ActorId { get; set; }
    public string Accion { get; set; } = string.Empty;
    public string Entidad { get; set; } = string.Empty;
    public long? EntidadId { get; set; }
    public string? Detalle { get; set; }
    public DateTime FechaUtc { get; set; }
}
