using Finvex.Domain;

namespace Finvex.Application;

public interface IFinancialEngineService
{
    decimal ConvertirATem(TipoTasa tipoTasa, decimal tasa);
    decimal CalcularTed(decimal tem);
    decimal CalcularCuotaFrancesa(decimal capital, decimal tem, int plazoMeses);
    IReadOnlyCollection<Cronograma> GenerarCronograma(Compra compra, Cliente cliente);
    (decimal Mora, decimal Interes, decimal Capital) AplicarPrelacion(decimal monto, decimal mora, decimal interes, decimal capital);
    decimal CalcularInteresDiario(decimal saldo, decimal ted, int dias);
    DateTime ObtenerFechaCorteCiclo(DateTime fecha, int diaCorte, TimeSpan horaCorte);
    DateTime ObtenerFechaPagoCiclo(DateTime fechaCorte, int diaCorte, int diaPago);
    DateTime ObtenerFechaPagoCompra(Compra compra, Cliente cliente);
    int CalcularDiasGracia(Compra compra, Cliente cliente);
    decimal CalcularTedCompensatoria(Cliente cliente);
    decimal CalcularTedMoratoria(Cliente cliente);
    decimal CalcularDeudaCapital(Cliente cliente);
    void ValidarCompra(Cliente cliente, decimal monto, ModalidadCompra modalidad, int plazoMeses);
    IReadOnlyCollection<ObligacionPendiente> ObtenerObligaciones(Compra compra, Cliente cliente, DateTime fecha);
    ResumenExigible CalcularExigible(Cliente cliente, DateTime fecha);
    ResumenExigible CalcularProximoPago(Cliente cliente, DateTime hoy);
    ResultadoPago CalcularPago(Cliente cliente, decimal monto, DateTime fechaPago);
    void ValidarProducto(Producto producto, long tiendaId, ModalidadCompra modalidad);
    DateTime ObtenerUltimoCorteCerrado(DateTime ahora, int diaCorte, TimeSpan horaCorte);
    ListadoPagoResponse CalcularListadoPago(Cliente cliente, DateTime fechaCorte, DateTime fechaCalculo);
}

public interface IClienteRepository
{
    Task<Cliente?> ObtenerConComprasAsync(long id, CancellationToken cancellationToken);
    Task GuardarAsync(Cliente cliente, CancellationToken cancellationToken);
    Task<IReadOnlyList<Cliente>> ListarPorTiendaAsync(long tiendaId, CancellationToken cancellationToken);
    Task<bool> ExisteDniAsync(long tiendaId, string dni, long excluirClienteId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Cliente>> ListarActivosConDiaCorteAsync(IReadOnlyCollection<int> diasCorte, CancellationToken cancellationToken);
}

public interface IClienteService
{
    Task<(Cliente? Cliente, string? Error)> ActualizarAsync(Cliente cliente, ActualizarClienteRequest request, CancellationToken cancellationToken);
    ClienteListItemResponse CrearItem(Cliente cliente);
    ClienteDetalleResponse CrearDetalle(Cliente cliente);
}

public interface ITiendaRepository
{
    Task<IReadOnlyList<Tienda>> ListarAsync(CancellationToken cancellationToken);
    Task<Tienda?> ObtenerAsync(long id, CancellationToken cancellationToken);
}

public interface IProductoRepository
{
    Task<IReadOnlyList<Producto>> ListarPorTiendaAsync(long tiendaId, bool incluirInactivos, CancellationToken cancellationToken);
    Task<Producto?> ObtenerAsync(long id, CancellationToken cancellationToken);
    Task AgregarAsync(Producto producto, CancellationToken cancellationToken);
}

public interface IListadoPagoRepository
{
    Task<ListadoPago?> ObtenerAsync(long clienteId, DateTime fechaCorte, CancellationToken cancellationToken);
    Task<bool> ExisteAsync(long clienteId, DateTime fechaCorte, CancellationToken cancellationToken);
    Task AgregarAsync(ListadoPago listado, CancellationToken cancellationToken);
}

public interface IListadoPagoService
{
    DateTime ResolverFechaCorte(Cliente cliente, DateTime? fechaCorte, DateTime ahora);
    ListadoPagoResponse Calcular(Cliente cliente, DateTime? fechaCorte, DateTime ahora);
    Task<(ListadoPago? Listado, bool Creado, string? Error)> GenerarAsync(Cliente cliente, DateTime? fechaCorte, DateTime ahora, CancellationToken cancellationToken);
    Task<int> GenerarCortesDelDiaAsync(DateTime ahora, CancellationToken cancellationToken);
    ListadoPagoResponse CrearRespuesta(ListadoPago listado);
}

public interface IAuditoriaService
{
    void Registrar(Operacion operacion, Action<Operacion>? completarAlGuardar = null);
}

public interface IAuditoriaRepository
{
    Task<(IReadOnlyList<Operacion> Items, int Total)> ListarAsync(long? tiendaId, DateTime? desdeUtc, DateTime? hastaUtc, string? accion,
        int pagina, int tamanoPagina, CancellationToken cancellationToken);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
