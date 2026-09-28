namespace Finvex.Domain;

public sealed class Tienda
{
    public long Id { get; set; }
    public string Ruc { get; set; } = string.Empty;
    public string RazonSocial { get; set; } = string.Empty;
    public string Giro { get; set; } = string.Empty;
    public string Usuario { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
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
