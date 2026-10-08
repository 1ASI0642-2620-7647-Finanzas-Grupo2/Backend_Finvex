using System.Security.Claims;
using Finvex.Application;
using Finvex.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finvex.API.Controllers;

[ApiController]
[Route("api/clientes/{clienteId:long}")]
[Authorize]
public sealed class CreditosController(
    IClienteRepository clientes,
    IUnitOfWork unitOfWork,
    IFinancialEngineService financialEngine,
    IProductoRepository productos,
    IAuditoriaService auditoria) : ControllerBase
{
    /// <summary>Registra una compra fiada y, si corresponde, genera su cronograma. Valida límite de crédito y plazo máximo del cliente. Sin fecha de compra se usa la fecha y hora actual de Lima. Con ProductoId, el precio es PrecioLista por Cantidad y el producto debe pertenecer a la tienda, estar activo y permitir la modalidad.</summary>
    [HttpPost("compras")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(CompraResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompraResponse>> CrearCompra(long clienteId, CrearCompraRequest request, CancellationToken cancellationToken)
    {
        var cliente = await clientes.ObtenerConComprasAsync(clienteId, cancellationToken);
        if (cliente is null) return NotFound("Cliente no encontrado.");
        if (!EsTiendaAutorizada(cliente)) return Forbid();
        if (!cliente.Activo) return BadRequest("El cliente esta inactivo.");
        if (request.Cantidad < 1) return BadRequest("La cantidad debe ser mayor que cero.");
        var descripcion = request.Producto?.Trim() ?? string.Empty;
        var precioCredito = request.PrecioCredito;
        try
        {
            if (request.ProductoId is long productoId)
            {
                var producto = await productos.ObtenerAsync(productoId, cancellationToken);
                if (producto is null) return NotFound("Producto no encontrado.");
                financialEngine.ValidarProducto(producto, cliente.TiendaId, request.Modalidad);
                precioCredito = producto.PrecioLista * request.Cantidad;
                descripcion = producto.Descripcion;
            }
            if (descripcion.Length is < ValidacionesEntrada.DescripcionProductoMinima or > ValidacionesEntrada.DescripcionProductoMaxima)
                return BadRequest($"La descripción del producto debe tener entre {ValidacionesEntrada.DescripcionProductoMinima} y {ValidacionesEntrada.DescripcionProductoMaxima} caracteres.");
            financialEngine.ValidarCompra(cliente, precioCredito, request.Modalidad, request.PlazoMeses);
        }
        catch (DomainException ex)
        {
            return BadRequest(ex.Message);
        }

        var compra = new Compra
        {
            ClienteId = clienteId,
            Producto = descripcion,
            PrecioCredito = decimal.Round(precioCredito, 2, MidpointRounding.AwayFromZero),
            SaldoCapital = decimal.Round(precioCredito, 2, MidpointRounding.AwayFromZero),
            Modalidad = request.Modalidad,
            PlazoMeses = request.Modalidad == ModalidadCompra.Cuotas ? request.PlazoMeses : 1,
            FechaCompra = request.FechaCompra.HasValue ? HoraLima.Normalizar(request.FechaCompra.Value) : HoraLima.Ahora,
            ProductoId = request.ProductoId,
            Cantidad = request.Cantidad,
            Estado = EstadoCompra.Pendiente
        };

        if (compra.Modalidad == ModalidadCompra.Cuotas)
        {
            foreach (var cuota in financialEngine.GenerarCronograma(compra, cliente)) compra.Cronogramas.Add(cuota);
            compra.SaldoCapital = compra.Cronogramas.Sum(x => x.Amortizacion);
        }
        cliente.Compras.Add(compra);
        await clientes.GuardarAsync(cliente, cancellationToken);
        auditoria.Registrar(User, AccionAuditoria.Compra, nameof(Compra), cliente.TiendaId,
            detalle: $"Cliente {cliente.Id}: {compra.Producto}, {compra.PrecioCredito}, {compra.Modalidad}, {compra.PlazoMeses} meses.",
            completarAlGuardar: operacion => operacion.EntidadId = compra.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created,
            new CompraResponse(compra.Id, compra.Producto, compra.PrecioCredito, compra.Modalidad, compra.Estado));
    }

    /// <summary>Obtiene el estado de cuenta del cliente: el exigible a hoy (hora Lima) si hay vencidos o, si no, el monto de la próxima fecha de pago. FechaCorte es el corte del ciclo abierto.</summary>
    [HttpGet("estado-cuenta")]
    [Authorize(Roles = "Admin,Cliente")]
    [ProducesResponseType(typeof(EstadoCuentaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EstadoCuentaResponse>> ObtenerEstadoCuenta(long clienteId, CancellationToken cancellationToken)
    {
        if (User.IsInRole("Cliente") && !EsClienteAutorizado(clienteId)) return Forbid();
        var cliente = await clientes.ObtenerConComprasAsync(clienteId, cancellationToken);
        if (cliente is null) return NotFound("Cliente no encontrado.");
        if (!EsTiendaAutorizada(cliente)) return Forbid();
        var ahora = HoraLima.Ahora;
        var resumen = financialEngine.CalcularProximoPago(cliente, ahora.Date);
        var fechaCorte = financialEngine.ObtenerFechaCorteCiclo(ahora, cliente.DiaCorte, cliente.HoraCorte);
        var items = cliente.Compras
            .Where(x => x.Estado != EstadoCompra.Pagada)
            .OrderBy(x => x.FechaCompra)
            .Select(compra => CrearEstado(compra, cliente, resumen.Fecha))
            .ToArray();
        return Ok(new EstadoCuentaResponse(clienteId, cliente.Moneda, fechaCorte, resumen.Total, items));
    }

    /// <summary>Lista el historial persistido de pagos del cliente, del más reciente al más antiguo.</summary>
    [HttpGet("pagos")]
    [Authorize(Roles = "Admin,Cliente")]
    [ProducesResponseType(typeof(IReadOnlyCollection<PagoHistorialResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<PagoHistorialResponse>>> ListarPagos(long clienteId, CancellationToken cancellationToken)
    {
        if (User.IsInRole("Cliente") && !EsClienteAutorizado(clienteId)) return Forbid();
        var cliente = await clientes.ObtenerConComprasAsync(clienteId, cancellationToken);
        if (cliente is null) return NotFound("Cliente no encontrado.");
        if (!EsTiendaAutorizada(cliente)) return Forbid();
        var pagos = await clientes.ListarPagosAsync(clienteId, cancellationToken);
        return Ok(pagos.Select(x => new PagoHistorialResponse(x.Id, x.MontoAbonado, x.FechaPago,
            x.ImputacionMora, x.ImputacionInteres, x.ImputacionCapital)).ToArray());
    }

    /// <summary>Registra un pago exacto por el total exigible a la fecha de pago (sin parciales ni excedentes) y lo imputa globalmente en el orden mora, interés compensatorio y capital.</summary>
    [HttpPost("pagos")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(PagoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagoResponse>> RegistrarPago(long clienteId, RegistrarPagoRequest request, CancellationToken cancellationToken)
    {
        var cliente = await clientes.ObtenerConComprasAsync(clienteId, cancellationToken);
        if (cliente is null) return NotFound("Cliente no encontrado.");
        if (!EsTiendaAutorizada(cliente)) return Forbid();
        if (request.Monto <= 0) return BadRequest("El monto debe ser mayor que cero.");

        var fechaPago = (request.FechaPago.HasValue ? HoraLima.Normalizar(request.FechaPago.Value) : HoraLima.Ahora).Date;
        ResultadoPago resultado;
        try
        {
            resultado = financialEngine.CalcularPago(cliente, request.Monto, fechaPago);
        }
        catch (DomainException ex)
        {
            return BadRequest(ex.Message);
        }

        foreach (var obligacion in resultado.Exigible.Obligaciones)
        {
            if (obligacion.Cuota is not null) obligacion.Cuota.Estado = EstadoCronograma.Pagada;
            obligacion.Compra.SaldoCapital = Math.Max(0m, obligacion.Compra.SaldoCapital - obligacion.Capital);
        }
        foreach (var compra in resultado.Exigible.Obligaciones.Select(x => x.Compra).Distinct())
        {
            if (compra.SaldoCapital > 0m && compra.Cronogramas.Any(x => x.Estado != EstadoCronograma.Pagada)) continue;
            compra.SaldoCapital = 0m;
            compra.Estado = EstadoCompra.Pagada;
        }

        var pago = new Pago
        {
            ClienteId = clienteId,
            MontoAbonado = resultado.Monto,
            FechaPago = fechaPago,
            ImputacionMora = resultado.ImputacionMora,
            ImputacionInteres = resultado.ImputacionInteres,
            ImputacionCapital = resultado.ImputacionCapital
        };
        cliente.Pagos.Add(pago);
        await clientes.GuardarAsync(cliente, cancellationToken);
        auditoria.Registrar(User, AccionAuditoria.Pago, nameof(Pago), cliente.TiendaId,
            detalle: $"Cliente {cliente.Id}: monto {pago.MontoAbonado}, mora {pago.ImputacionMora}, interés {pago.ImputacionInteres}, capital {pago.ImputacionCapital}.",
            completarAlGuardar: operacion => operacion.EntidadId = pago.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok(new PagoResponse(pago.MontoAbonado, pago.ImputacionMora, pago.ImputacionInteres, pago.ImputacionCapital));
    }

    private EstadoCuentaItemResponse CrearEstado(Compra compra, Cliente cliente, DateTime fecha)
    {
        var exigibles = financialEngine.ObtenerObligaciones(compra, cliente, fecha)
            .Where(x => x.FechaVencimiento <= fecha.Date)
            .ToArray();
        var estado = exigibles.Any(x => x.DiasMora > 0) ? EstadoCompra.Mora : compra.Estado;
        var cuotas = compra.Cronogramas
            .Where(x => x.Estado != EstadoCronograma.Pagada)
            .OrderBy(x => x.NroCuota)
            .Select(x => new CuotaResponse(x.NroCuota, x.FechaVencimiento, x.CuotaFija, x.Interes, x.Amortizacion,
                x.FechaVencimiento.Date < fecha.Date ? EstadoCronograma.Mora : x.Estado))
            .ToArray();
        return new EstadoCuentaItemResponse(compra.Id, compra.Producto, compra.SaldoCapital, exigibles.Sum(x => x.Interes),
            exigibles.Sum(x => x.Mora), exigibles.Sum(x => x.Total), estado, cuotas);
    }

    private bool EsClienteAutorizado(long clienteId) =>
        long.TryParse(User.FindFirst("ClienteId")?.Value, out var tokenClienteId) && tokenClienteId == clienteId;

    private bool EsTiendaAutorizada(Cliente cliente) =>
        long.TryParse(User.FindFirstValue("TiendaId"), out var tiendaId) && tiendaId == cliente.TiendaId;
}
