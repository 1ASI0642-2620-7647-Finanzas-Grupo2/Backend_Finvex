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
    IFinancialEngineService financialEngine) : ControllerBase
{
    /// <summary>Registra una compra fiada y, si corresponde, genera su cronograma.</summary>
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
        if (request.PrecioCredito <= 0) return BadRequest("El precio debe ser mayor que cero.");
        if (request.Modalidad == ModalidadCompra.Cuotas && request.PlazoMeses <= 0) return BadRequest("El plazo es obligatorio para cuotas.");

        var deudaCapital = cliente.Compras.Where(x => x.Estado != EstadoCompra.Pagada).Sum(x => x.SaldoCapital);
        var disponible = cliente.LimiteCredito - deudaCapital;
        if (request.PrecioCredito > disponible)
            return BadRequest(new CreditLimitExceededException(request.PrecioCredito, disponible).Message);

        var compra = new Compra
        {
            ClienteId = clienteId,
            Producto = request.Producto.Trim(),
            PrecioCredito = decimal.Round(request.PrecioCredito, 2),
            SaldoCapital = decimal.Round(request.PrecioCredito, 2),
            Modalidad = request.Modalidad,
            PlazoMeses = request.Modalidad == ModalidadCompra.Cuotas ? request.PlazoMeses : 1,
            FechaCompra = (request.FechaCompra ?? DateTime.UtcNow).Date,
            Estado = EstadoCompra.Pendiente
        };

        if (compra.Modalidad == ModalidadCompra.Cuotas)
        {
            foreach (var cuota in financialEngine.GenerarCronograma(compra, cliente)) compra.Cronogramas.Add(cuota);
            compra.SaldoCapital = compra.Cronogramas.Sum(x => x.Amortizacion);
        }
        cliente.Compras.Add(compra);
        await clientes.GuardarAsync(cliente, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created,
            new CompraResponse(compra.Id, compra.Producto, compra.PrecioCredito, compra.Modalidad, compra.Estado));
    }

    /// <summary>Obtiene el estado de cuenta exigible del cliente a la fecha actual.</summary>
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
        var fecha = DateTime.UtcNow.Date;
        var items = cliente.Compras.Where(x => x.Estado != EstadoCompra.Pagada).Select(compra => CrearEstado(compra, cliente, fecha)).ToArray();
        return Ok(new EstadoCuentaResponse(clienteId, fecha, items.Sum(x => x.TotalExigible), items));
    }

    /// <summary>Registra un pago y lo imputa por mora, interes compensatorio y capital.</summary>
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

        var restante = decimal.Round(request.Monto, 2);
        var imputacionMora = 0m;
        var imputacionInteres = 0m;
        var imputacionCapital = 0m;
        var fechaPago = (request.FechaPago ?? DateTime.UtcNow).Date;

        foreach (var compra in cliente.Compras.Where(x => x.Estado != EstadoCompra.Pagada).OrderBy(x => x.FechaCompra))
        {
            if (restante <= 0) break;
            var estado = CrearEstado(compra, cliente, fechaPago);
            var aplicacion = financialEngine.AplicarPrelacion(restante, estado.InteresMoratorio, estado.InteresCompensatorio, estado.CapitalPendiente);
            imputacionMora += aplicacion.Mora;
            imputacionInteres += aplicacion.Interes;
            imputacionCapital += aplicacion.Capital;
            restante -= aplicacion.Mora + aplicacion.Interes + aplicacion.Capital;
            compra.SaldoCapital = Math.Max(0m, compra.SaldoCapital - aplicacion.Capital);
            AplicarAcronograma(compra, aplicacion.Interes, aplicacion.Capital);
            if (compra.SaldoCapital == 0m) compra.Estado = EstadoCompra.Pagada;
        }

        var pago = new Pago
        {
            ClienteId = clienteId,
            MontoAbonado = request.Monto - restante,
            FechaPago = fechaPago,
            ImputacionMora = imputacionMora,
            ImputacionInteres = imputacionInteres,
            ImputacionCapital = imputacionCapital
        };
        cliente.Pagos.Add(pago);
        await clientes.GuardarAsync(cliente, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok(new PagoResponse(pago.MontoAbonado, imputacionMora, imputacionInteres, imputacionCapital));
    }

    private EstadoCuentaItemResponse CrearEstado(Compra compra, Cliente cliente, DateTime fecha)
    {
        var tem = financialEngine.ConvertirATem(cliente.TipoTasa, cliente.TasaCompensatoria);
        var ted = financialEngine.CalcularTed(tem);
        var tedMora = financialEngine.CalcularTed(financialEngine.ConvertirATem(cliente.TipoTasa, cliente.TasaMoratoria));
        var cuotas = compra.Cronogramas.Where(x => x.Estado != EstadoCronograma.Pagada).OrderBy(x => x.NroCuota).ToArray();
        var capital = compra.SaldoCapital;
        var interes = 0m;
        var mora = 0m;
        foreach (var cuota in cuotas)
        {
            var fechaInteres = fecha < cuota.FechaVencimiento ? fecha : cuota.FechaVencimiento;
            interes += financialEngine.CalcularInteresDiario(cuota.SaldoInicial, ted, Math.Max(0, (fechaInteres - compra.FechaCompra).Days));
            if (fecha > cuota.FechaVencimiento) mora += financialEngine.CalcularInteresDiario(cuota.SaldoInicial, tedMora, (fecha - cuota.FechaVencimiento).Days);
        }
        if (cuotas.Length == 0)
        {
            var vencimiento = ObtenerFechaPago(compra.FechaCompra, cliente.DiaPago);
            var fechaFinInteres = fecha < vencimiento ? fecha : vencimiento;
            interes = financialEngine.CalcularInteresDiario(capital, ted, Math.Max(0, (fechaFinInteres - compra.FechaCompra).Days));
            if (fecha > vencimiento) mora = financialEngine.CalcularInteresDiario(capital, tedMora, (fecha - vencimiento).Days);
        }
        var cuotaResponse = cuotas.Select(x => new CuotaResponse(x.NroCuota, x.FechaVencimiento, x.CuotaFija, x.Interes, x.Amortizacion, x.Estado)).ToArray();
        return new EstadoCuentaItemResponse(compra.Id, compra.Producto, capital, decimal.Round(interes, 2), decimal.Round(mora, 2), decimal.Round(capital + interes + mora, 2), compra.Estado, cuotaResponse);
    }

    private static void AplicarAcronograma(Compra compra, decimal interes, decimal capital)
    {
        foreach (var cuota in compra.Cronogramas.Where(x => x.Estado != EstadoCronograma.Pagada).OrderBy(x => x.NroCuota))
        {
            var interesAplicado = Math.Min(interes, cuota.Interes);
            cuota.Interes -= interesAplicado;
            interes -= interesAplicado;
            var capitalAplicado = Math.Min(capital, cuota.Amortizacion);
            cuota.Amortizacion -= capitalAplicado;
            capital -= capitalAplicado;
            if (cuota.Interes <= 0m && cuota.Amortizacion <= 0m) cuota.Estado = EstadoCronograma.Pagada;
            if (interes <= 0m && capital <= 0m) break;
        }
    }

    private static DateTime ObtenerFechaPago(DateTime fecha, int dia)
    {
        var pago = new DateTime(fecha.Year, fecha.Month, Math.Min(dia, DateTime.DaysInMonth(fecha.Year, fecha.Month)));
        return fecha <= pago ? pago : pago.AddMonths(1);
    }

    private bool EsClienteAutorizado(long clienteId) =>
        long.TryParse(User.FindFirst("ClienteId")?.Value, out var tokenClienteId) && tokenClienteId == clienteId;

    private bool EsTiendaAutorizada(Cliente cliente) =>
        User.IsInRole("Cliente") || (long.TryParse(User.FindFirstValue("TiendaId"), out var tiendaId) && tiendaId == cliente.TiendaId);
}
