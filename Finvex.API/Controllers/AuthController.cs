using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Finvex.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace Finvex.API.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public sealed class AuthController(IAuthService authService, IUnitOfWork unitOfWork, IConfiguration configuration) : ControllerBase
{
    /// <summary>Registra una nueva tienda con una contraseña BCrypt.</summary>
    [HttpPost("register/admin")]
    [ProducesResponseType(typeof(RegistroResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RegistroResponse>> RegistrarAdmin(RegistrarAdminRequest request, CancellationToken cancellationToken)
    {
        var resultado = await authService.RegistrarTiendaAsync(request, cancellationToken);
        if (resultado.Tienda is null) return BadRequest(resultado.Error);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return StatusCode(StatusCodes.Status201Created,
            new RegistroResponse(resultado.Tienda.Id, resultado.Tienda.Usuario, "Tienda registrada correctamente."));
    }

    /// <summary>Autentica una tienda y devuelve un token con rol Admin.</summary>
    [HttpPost("login/admin")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> LoginAdmin(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await authService.AutenticarAdminAsync(request.Usuario, request.Password, cancellationToken);
        return user is null ? Unauthorized("Usuario o contraseña inválidos.") : Ok(CrearRespuesta(user));
    }

    /// <summary>Autentica un cliente y devuelve un token con rol Cliente.</summary>
    [HttpPost("login/cliente")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> LoginCliente(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await authService.AutenticarClienteAsync(request.Usuario, request.Password, cancellationToken);
        return user is null ? Unauthorized("Usuario o contraseña inválidos.") : Ok(CrearRespuesta(user));
    }

    private LoginResponse CrearRespuesta(AuthenticatedUser user)
    {
        var jwt = configuration.GetSection("Jwt");
        var key = jwt["Key"] ?? throw new InvalidOperationException("No se configuró Jwt:Key.");
        var issuer = jwt["Issuer"] ?? "Finvex";
        var audience = jwt["Audience"] ?? "Finvex.Clients";
        var expiration = DateTime.UtcNow.AddMinutes(int.TryParse(jwt["ExpirationMinutes"], out var minutes) ? minutes : 60);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Usuario),
            new Claim(ClaimTypes.Role, user.Rol),
            new Claim(user.Rol == "Admin" ? "TiendaId" : "ClienteId", user.ContextId.ToString())
        };
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(issuer, audience, claims, expires: expiration, signingCredentials: credentials);
        return new LoginResponse(new JwtSecurityTokenHandler().WriteToken(token), user.Rol, user.Id,
            user.Rol == "Admin" ? user.ContextId : null, user.Rol == "Cliente" ? user.ContextId : null, expiration);
    }
}
