using Finvex.Domain;

namespace Finvex.Application;

public sealed record RegistrarAdminRequest(string Ruc, string RazonSocial, string Giro, string Usuario, string Password);
public sealed record RegistrarClienteRequest(string Dni, string Nombres, decimal LimiteCredito, TipoTasa TipoTasa, decimal TasaCompensatoria, decimal TasaMoratoria, int DiaCorte, int DiaPago, string Usuario, string Password);
public sealed record RegistroResponse(long Id, string Usuario, string Mensaje);
public sealed record CrearCompraRequest(string Producto, decimal PrecioCredito, ModalidadCompra Modalidad, int PlazoMeses, DateTime? FechaCompra);
public sealed record RegistrarPagoRequest(decimal Monto, DateTime? FechaPago);
public sealed record CompraResponse(long Id, string Producto, decimal PrecioCredito, ModalidadCompra Modalidad, EstadoCompra Estado);
public sealed record CuotaResponse(int Numero, DateTime Vencimiento, decimal Cuota, decimal Interes, decimal Amortizacion, EstadoCronograma Estado);
public sealed record EstadoCuentaItemResponse(long CompraId, string Producto, decimal CapitalPendiente, decimal InteresCompensatorio, decimal InteresMoratorio, decimal TotalExigible, EstadoCompra Estado, IReadOnlyCollection<CuotaResponse> Cuotas);
public sealed record EstadoCuentaResponse(long ClienteId, DateTime FechaCorte, decimal TotalExigible, IReadOnlyCollection<EstadoCuentaItemResponse> Compras);
public sealed record PagoResponse(decimal Monto, decimal ImputacionMora, decimal ImputacionInteres, decimal ImputacionCapital);
