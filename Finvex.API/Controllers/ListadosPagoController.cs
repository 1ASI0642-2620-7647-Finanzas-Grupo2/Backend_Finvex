using System.Security.Claims;
using Finvex.Application;
using Finvex.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finvex.API.Controllers;

[ApiController]
[Route("api/clientes/{clienteId:long}/listado-pago")]
[Authorize]
public sealed class ListadosPagoController(
    IClienteRepository clientes,
    IListadoPagoService listadoPagoService,
    IUnitOfWork unitOfWork) : ControllerBase
{
    /// <summary>Calcula el listado de pago de un ciclo: compras del ciclo ordenadas por fecha (capital, días e interés compensatorio), cuotas que vencen en la fecha de pago y un ítem "Intereses por mora" si hay días de mora a hoy (hora Lima). Si fechaCorte se omite se usa el último corte cerrado; cualquier fecha se asocia al corte de su ciclo.</summary>
    [HttpGet]
    [Authorize(Roles = "Admin,Cliente")]
    [ProducesResponseType(typeof(ListadoPagoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ListadoPagoResponse>> Obtener(long clienteId, [FromQuery] DateTime? fechaCorte, CancellationToken cancellationToken)
    {
        if (User.IsInRole("Cliente") && !EsClienteAutorizado(clienteId)) return Forbid();
        var cliente = await clientes.ObtenerConComprasAsync(clienteId, cancellationToken);
        if (cliente is null) return NotFound("Cliente no encontrado.");
        if (!EsTiendaAutorizada(cliente)) return Forbid();
        return Ok(listadoPagoService.Calcular(cliente, fechaCorte, HoraLima.Ahora));
    }

    /// <summary>Genera y guarda el listado de pago de un ciclo cerrado. Es idempotente: si ya existe para ese corte devuelve el guardado con 200; si lo crea devuelve 201.</summary>
    [HttpPost("generar")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ListadoPagoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ListadoPagoResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ListadoPagoResponse>> Generar(long clienteId, [FromQuery] DateTime? fechaCorte, CancellationToken cancellationToken)
    {
        var cliente = await clientes.ObtenerConComprasAsync(clienteId, cancellationToken);
        if (cliente is null) return NotFound("Cliente no encontrado.");
        if (!EsTiendaAutorizada(cliente)) return Forbid();
        var resultado = await listadoPagoService.GenerarAsync(cliente, fechaCorte, HoraLima.Ahora, cancellationToken);
        if (resultado.Listado is null) return BadRequest(resultado.Error);
        if (!resultado.Creado) return Ok(listadoPagoService.CrearRespuesta(resultado.Listado));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return StatusCode(StatusCodes.Status201Created, listadoPagoService.CrearRespuesta(resultado.Listado));
    }

    private bool EsClienteAutorizado(long clienteId) =>
        long.TryParse(User.FindFirst("ClienteId")?.Value, out var tokenClienteId) && tokenClienteId == clienteId;

    private bool EsTiendaAutorizada(Cliente cliente) =>
        long.TryParse(User.FindFirstValue("TiendaId"), out var tiendaId) && tiendaId == cliente.TiendaId;
}
