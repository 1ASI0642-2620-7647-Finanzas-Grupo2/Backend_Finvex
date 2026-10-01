using Finvex.Application;
using Finvex.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finvex.API.Controllers;

[ApiController]
[Route("api/sistema/tiendas")]
[Authorize(Roles = "AdminSistema")]
public sealed class SistemaTiendasController(
    ITiendaRepository tiendas,
    IAuthService authService,
    IUnitOfWork unitOfWork,
    IAuditoriaService auditoria) : ControllerBase
{
    /// <summary>Lista todas las tiendas registradas en el sistema.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<TiendaResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<TiendaResponse>>> Listar(CancellationToken cancellationToken)
    {
        var lista = await tiendas.ListarAsync(cancellationToken);
        return Ok(lista.Select(CrearRespuesta).ToArray());
    }

    /// <summary>Registra una tienda con su RUC, razón social, giro, usuario y contraseña.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(TiendaResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TiendaResponse>> Registrar(RegistrarAdminRequest request, CancellationToken cancellationToken)
    {
        var resultado = await authService.RegistrarTiendaAsync(request, cancellationToken);
        if (resultado.Tienda is null) return BadRequest(resultado.Error);
        var tienda = resultado.Tienda;
        auditoria.Registrar(User, AccionAuditoria.Alta, nameof(Tienda), null, detalle: $"RUC {tienda.Ruc}, usuario {tienda.Usuario}.",
            completarAlGuardar: operacion => operacion.TiendaId = operacion.EntidadId = tienda.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return StatusCode(StatusCodes.Status201Created, CrearRespuesta(resultado.Tienda));
    }

    /// <summary>Da de baja lógica a una tienda: su administrador no podrá iniciar sesión.</summary>
    [HttpPut("{id:long}/baja")]
    [ProducesResponseType(typeof(TiendaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<TiendaResponse>> DarDeBaja(long id, CancellationToken cancellationToken) =>
        CambiarEstado(id, false, cancellationToken);

    /// <summary>Reactiva una tienda dada de baja.</summary>
    [HttpPut("{id:long}/alta")]
    [ProducesResponseType(typeof(TiendaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<TiendaResponse>> DarDeAlta(long id, CancellationToken cancellationToken) =>
        CambiarEstado(id, true, cancellationToken);

    private async Task<ActionResult<TiendaResponse>> CambiarEstado(long id, bool activo, CancellationToken cancellationToken)
    {
        var tienda = await tiendas.ObtenerAsync(id, cancellationToken);
        if (tienda is null) return NotFound("Tienda no encontrada.");
        tienda.Activo = activo;
        auditoria.Registrar(User, activo ? AccionAuditoria.Reactivacion : AccionAuditoria.Baja, nameof(Tienda), tienda.Id, tienda.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok(CrearRespuesta(tienda));
    }

    private static TiendaResponse CrearRespuesta(Tienda tienda) => new(
        tienda.Id, tienda.Ruc, tienda.RazonSocial, tienda.Giro, tienda.Usuario, tienda.Activo, tienda.Activo ? "Activo" : "Inactivo");
}
