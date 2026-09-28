using System.Security.Claims;
using Finvex.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Finvex.API.Controllers;

[ApiController]
[Route("api/clientes")]
[Authorize(Roles = "Admin")]
public sealed class ClientesController(IAuthService authService, IUnitOfWork unitOfWork) : ControllerBase
{
    /// <summary>Registra un cliente vinculado a la tienda del administrador autenticado.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(RegistroResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<RegistroResponse>> RegistrarCliente(RegistrarClienteRequest request, CancellationToken cancellationToken)
    {
        if (!long.TryParse(User.FindFirstValue("TiendaId"), out var tiendaId)) return Unauthorized();
        var resultado = await authService.RegistrarClienteAsync(tiendaId, request, cancellationToken);
        if (resultado.Cliente is null) return BadRequest(resultado.Error);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return StatusCode(StatusCodes.Status201Created,
            new RegistroResponse(resultado.Cliente.Id, resultado.Cliente.Usuario, "Cliente registrado correctamente."));
    }
}