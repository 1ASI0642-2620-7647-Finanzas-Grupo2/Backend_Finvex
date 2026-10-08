using Finvex.Domain;

namespace Finvex.Application;

public sealed record RegistrarAdminRequest(string Ruc, string RazonSocial, string Giro, string Usuario, string Password);
public sealed record RegistrarClienteRequest(string Dni, string Nombres, decimal LimiteCredito, TipoTasa TipoTasa, decimal TasaCompensatoria, decimal TasaMoratoria, int DiaCorte, int DiaPago, string Usuario, string Password, Moneda Moneda = Moneda.PEN, int MaxMeses = 1, TimeSpan? HoraCorte = null);
public sealed record RegistroResponse(long Id, string Usuario, string Mensaje);
public sealed record CrearCompraRequest(string Producto, decimal PrecioCredito, ModalidadCompra Modalidad, int PlazoMeses, DateTime? FechaCompra, long? ProductoId = null, int Cantidad = 1);
public sealed record RegistrarPagoRequest(decimal Monto, DateTime? FechaPago);
public sealed record CompraResponse(long Id, string Producto, decimal PrecioCredito, ModalidadCompra Modalidad, EstadoCompra Estado);
public sealed record CuotaResponse(int Numero, DateTime Vencimiento, decimal Cuota, decimal Interes, decimal Amortizacion, EstadoCronograma Estado);
public sealed record EstadoCuentaItemResponse(long CompraId, string Producto, decimal CapitalPendiente, decimal InteresCompensatorio, decimal InteresMoratorio, decimal TotalExigible, EstadoCompra Estado, IReadOnlyCollection<CuotaResponse> Cuotas);
public sealed record EstadoCuentaResponse(long ClienteId, Moneda Moneda, DateTime FechaCorte, decimal TotalExigible, IReadOnlyCollection<EstadoCuentaItemResponse> Compras);
public sealed record PagoResponse(decimal Monto, decimal ImputacionMora, decimal ImputacionInteres, decimal ImputacionCapital);
public sealed record PagoHistorialResponse(long Id, decimal Monto, DateTime FechaPago, decimal ImputacionMora, decimal ImputacionInteres, decimal ImputacionCapital);
public sealed record ClienteListItemResponse(long ClienteId, string Dni, string Nombres, decimal LimiteCredito, string Estado, decimal DeudaActual);
public sealed record ClienteDetalleResponse(long ClienteId, string Dni, string Nombres, string Usuario, decimal LimiteCredito, decimal CreditoDisponible, TipoTasa TipoTasa, decimal TasaCompensatoria, decimal TasaMoratoria, int DiaCorte, int DiaPago, Moneda Moneda, int MaxMeses, TimeSpan HoraCorte, string Estado, decimal DeudaActual);
public sealed record ActualizarClienteRequest(string Dni, string Nombres, decimal LimiteCredito, TipoTasa TipoTasa, decimal TasaCompensatoria, decimal TasaMoratoria, int DiaCorte, int DiaPago, Moneda? Moneda = null, int? MaxMeses = null, TimeSpan? HoraCorte = null, string? Password = null);

public sealed record ObligacionPendiente(Compra Compra, Cronograma? Cuota, DateTime FechaVencimiento, decimal Capital, decimal Interes, int DiasInteres, decimal Mora, int DiasMora)
{
    public decimal Base => Capital + Interes;
    public decimal Total => Capital + Interes + Mora;
}

public sealed record ResumenExigible(DateTime Fecha, IReadOnlyCollection<ObligacionPendiente> Obligaciones, decimal Mora, decimal Interes, decimal Capital, decimal Total);
public sealed record ResultadoPago(ResumenExigible Exigible, decimal Monto, decimal ImputacionMora, decimal ImputacionInteres, decimal ImputacionCapital);

public sealed record TiendaResponse(long Id, string Ruc, string RazonSocial, string Giro, string Usuario, bool Activo, string Estado);
public sealed record ProductoRequest(string Marca, string Descripcion, string UnidadMedida, decimal PrecioContado, decimal PrecioLista, bool PermiteFinDeMes = true, bool PermiteCuotas = false, string? Proveedor = null);
public sealed record ProductoResponse(long Id, string? Proveedor, string Marca, string Descripcion, string UnidadMedida, string? ImagenUrl, decimal PrecioContado, decimal PrecioLista, bool PermiteFinDeMes, bool PermiteCuotas, bool Activo);

public sealed record ItemListadoPagoResponse(TipoItemListado Tipo, long? CompraId, int? NroCuota, string Descripcion, DateTime Fecha, decimal Capital, int Dias, decimal InteresCompensatorio, decimal Monto);
public sealed record ListadoPagoResponse(long ClienteId, DateTime FechaCorte, DateTime FechaPago, DateTime FechaCalculo, decimal Total, IReadOnlyCollection<ItemListadoPagoResponse> Items, long? ListadoPagoId = null, DateTime? FechaGeneracionUtc = null);
public sealed record OperacionResponse(long Id, long? TiendaId, string ActorRol, long? ActorId, string Accion, string Entidad, long? EntidadId, string? Detalle, DateTime FechaUtc, DateTime FechaLima);
public sealed record PaginaResponse<T>(IReadOnlyCollection<T> Items, int Pagina, int TamanoPagina, int TotalRegistros);
