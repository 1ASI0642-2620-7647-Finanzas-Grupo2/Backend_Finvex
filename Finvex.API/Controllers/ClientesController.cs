using System.Security.Claims;
using Finvex.Application;
using Finvex.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finvex.API.Controllers;

[ApiController]
[Route("api/clientes")]
[Authorize(Roles = "Admin")]
public sealed class ClientesController(
    IAuthService authService,
    IUnitOfWork unitOfWork,
    IClienteRepository clientes,
    IClienteService clienteService,
    IAuditoriaService auditoria) : ControllerBase
{
    /// <summary>Registra un cliente vinculado a la tienda del administrador autenticado. Las tasas se envían como fracción (0.05 = 5 %); Moneda (PEN o USD), MaxMeses (1 a 36) y HoraCorte (hh:mm:ss) son opcionales.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(RegistroResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<RegistroResponse>> RegistrarCliente(RegistrarClienteRequest request, CancellationToken cancellationToken)
    {
        if (!long.TryParse(User.FindFirstValue("TiendaId"), out var tiendaId)) return Unauthorized();
        var resultado = await authService.RegistrarClienteAsync(tiendaId, request, cancellationToken);
        if (resultado.Cliente is null) return BadRequest(resultado.Error);
        var cliente = resultado.Cliente;
        auditoria.Registrar(User, AccionAuditoria.Alta, nameof(Cliente), tiendaId, detalle: $"DNI {cliente.Dni}, usuario {cliente.Usuario}.",
            completarAlGuardar: operacion => operacion.EntidadId = cliente.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return StatusCode(StatusCodes.Status201Created,
            new RegistroResponse(resultado.Cliente.Id, resultado.Cliente.Usuario, "Cliente registrado correctamente."));
    }

    /// <summary>Lista los clientes de la tienda del administrador autenticado con su deuda de capital pendiente.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<ClienteListItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyCollection<ClienteListItemResponse>>> Listar(CancellationToken cancellationToken)
    {
        if (!long.TryParse(User.FindFirstValue("TiendaId"), out var tiendaId)) return Unauthorized();
        var lista = await clientes.ListarPorTiendaAsync(tiendaId, cancellationToken);
        return Ok(lista.Select(clienteService.CrearItem).ToArray());
    }

    /// <summary>Obtiene los datos y condiciones de crédito de un cliente de la tienda. Las tasas se expresan como fracción (0.05 = 5 %).</summary>
    [HttpGet("{clienteId:long}")]
    [ProducesResponseType(typeof(ClienteDetalleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClienteDetalleResponse>> Obtener(long clienteId, CancellationToken cancellationToken)
    {
        var cliente = await clientes.ObtenerConComprasAsync(clienteId, cancellationToken);
        if (cliente is null) return NotFound("Cliente no encontrado.");
        if (!EsTiendaAutorizada(cliente)) return Forbid();
        return Ok(clienteService.CrearDetalle(cliente));
    }

    /// <summary>Actualiza los datos y condiciones de crédito de un cliente. Las tasas se envían como fracción (0.05 = 5 %); Moneda, MaxMeses, HoraCorte y Password son opcionales y, si se omiten, se conservan.</summary>
    [HttpPut("{clienteId:long}")]
    [ProducesResponseType(typeof(ClienteDetalleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClienteDetalleResponse>> Actualizar(long clienteId, ActualizarClienteRequest request, CancellationToken cancellationToken)
    {
        var cliente = await clientes.ObtenerConComprasAsync(clienteId, cancellationToken);
        if (cliente is null) return NotFound("Cliente no encontrado.");
        if (!EsTiendaAutorizada(cliente)) return Forbid();
        var resultado = await clienteService.ActualizarAsync(cliente, request, cancellationToken);
        if (resultado.Cliente is null) return BadRequest(resultado.Error);
        auditoria.Registrar(User, AccionAuditoria.Edicion, nameof(Cliente), cliente.TiendaId, cliente.Id,
            $"Límite {cliente.LimiteCredito}, tasa {cliente.TipoTasa} {cliente.TasaCompensatoria}, mora {cliente.TasaMoratoria}, corte {cliente.DiaCorte}, pago {cliente.DiaPago}, máx. {cliente.MaxMeses} meses.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok(clienteService.CrearDetalle(cliente));
    }

    /// <summary>Da de baja lógica a un cliente: no podrá iniciar sesión ni registrar compras.</summary>
    [HttpPut("{clienteId:long}/baja")]
    [ProducesResponseType(typeof(ClienteDetalleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<ClienteDetalleResponse>> DarDeBaja(long clienteId, CancellationToken cancellationToken) =>
        CambiarEstado(clienteId, false, cancellationToken);

    /// <summary>Reactiva a un cliente dado de baja.</summary>
    [HttpPut("{clienteId:long}/alta")]
    [ProducesResponseType(typeof(ClienteDetalleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<ClienteDetalleResponse>> DarDeAlta(long clienteId, CancellationToken cancellationToken) =>
        CambiarEstado(clienteId, true, cancellationToken);

    private async Task<ActionResult<ClienteDetalleResponse>> CambiarEstado(long clienteId, bool activo, CancellationToken cancellationToken)
    {
        var cliente = await clientes.ObtenerConComprasAsync(clienteId, cancellationToken);
        if (cliente is null) return NotFound("Cliente no encontrado.");
        if (!EsTiendaAutorizada(cliente)) return Forbid();
        cliente.Activo = activo;
        await clientes.GuardarAsync(cliente, cancellationToken);
        auditoria.Registrar(User, activo ? AccionAuditoria.Reactivacion : AccionAuditoria.Baja, nameof(Cliente), cliente.TiendaId, cliente.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok(clienteService.CrearDetalle(cliente));
    }

    private bool EsTiendaAutorizada(Cliente cliente) =>
        long.TryParse(User.FindFirstValue("TiendaId"), out var tiendaId) && tiendaId == cliente.TiendaId;
}
