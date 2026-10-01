using System.Security.Claims;
using Finvex.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finvex.API.Controllers;

[ApiController]
[Route("api/auditoria")]
[Authorize(Roles = "Admin,AdminSistema")]
public sealed class AuditoriaController(IAuditoriaRepository auditoria) : ControllerBase
{
    private const int TamanoPaginaMaximo = 100;

    /// <summary>Lista las operaciones auditadas, de la más reciente a la más antigua. El Admin solo ve las de su tienda; el AdminSistema ve todas. Las fechas desde y hasta se interpretan en hora de Lima y ambas son inclusivas por día; accion filtra por valor exacto (LoginCorrecto, LoginFallido, Alta, Edicion, Baja, Reactivacion, Compra, Pago).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginaResponse<OperacionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaginaResponse<OperacionResponse>>> Listar(
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        [FromQuery] string? accion,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanoPagina = 20,
        CancellationToken cancellationToken = default)
    {
        if (pagina < 1 || tamanoPagina is < 1 or > TamanoPaginaMaximo)
            return BadRequest($"La página debe ser mayor que cero y el tamaño de página debe estar entre 1 y {TamanoPaginaMaximo}.");
        long? tiendaId = null;
        if (!User.IsInRole("AdminSistema"))
        {
            if (!long.TryParse(User.FindFirstValue("TiendaId"), out var tiendaUsuario)) return Unauthorized();
            tiendaId = tiendaUsuario;
        }
        var desdeUtc = desde.HasValue ? HoraLima.AUtc(desde.Value.Date) : (DateTime?)null;
        var hastaUtc = hasta.HasValue ? HoraLima.AUtc(hasta.Value.Date.AddDays(1)) : (DateTime?)null;
        var (items, total) = await auditoria.ListarAsync(tiendaId, desdeUtc, hastaUtc, accion?.Trim(), pagina, tamanoPagina, cancellationToken);
        var respuesta = items.Select(x => new OperacionResponse(x.Id, x.TiendaId, x.ActorRol, x.ActorId, x.Accion, x.Entidad, x.EntidadId,
            x.Detalle, DateTime.SpecifyKind(x.FechaUtc, DateTimeKind.Utc), HoraLima.DesdeUtc(x.FechaUtc))).ToArray();
        return Ok(new PaginaResponse<OperacionResponse>(respuesta, pagina, tamanoPagina, total));
    }
}
