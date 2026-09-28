using BCrypt.Net;
using Finvex.Domain;

namespace Finvex.Application;

public sealed record LoginRequest(string Usuario, string Password);
public sealed record AuthenticatedUser(long Id, string Usuario, string Rol, long ContextId);
public sealed record LoginResponse(string Token, string Rol, long Id, long? TiendaId, long? ClienteId, DateTime ExpiraEn);

public interface IAuthRepository
{
    Task<Tienda?> ObtenerTiendaPorUsuarioAsync(string usuario, CancellationToken cancellationToken);
    Task<Cliente?> ObtenerClientePorUsuarioAsync(string usuario, CancellationToken cancellationToken);
    Task<bool> ExisteTiendaAsync(string usuario, string ruc, CancellationToken cancellationToken);
    Task<bool> ExisteClienteAsync(long tiendaId, string usuario, string dni, CancellationToken cancellationToken);
    Task AgregarTiendaAsync(Tienda tienda, CancellationToken cancellationToken);
    Task AgregarClienteAsync(Cliente cliente, CancellationToken cancellationToken);
}

public interface IAuthService
{
    Task<(Tienda? Tienda, string? Error)> RegistrarTiendaAsync(RegistrarAdminRequest request, CancellationToken cancellationToken);
    Task<(Cliente? Cliente, string? Error)> RegistrarClienteAsync(long tiendaId, RegistrarClienteRequest request, CancellationToken cancellationToken);
    Task<AuthenticatedUser?> AutenticarAdminAsync(string usuario, string password, CancellationToken cancellationToken);
    Task<AuthenticatedUser?> AutenticarClienteAsync(string usuario, string password, CancellationToken cancellationToken);
}

public sealed class AuthService(IAuthRepository authRepository) : IAuthService
{
    public async Task<(Tienda? Tienda, string? Error)> RegistrarTiendaAsync(RegistrarAdminRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Ruc) || request.Ruc.Trim().Length != 11 ||
            string.IsNullOrWhiteSpace(request.RazonSocial) || string.IsNullOrWhiteSpace(request.Giro) ||
            string.IsNullOrWhiteSpace(request.Usuario) || string.IsNullOrWhiteSpace(request.Password))
            return (null, "RUC, razón social, giro, usuario y contraseña son obligatorios; el RUC debe tener 11 caracteres.");
        if (await authRepository.ExisteTiendaAsync(request.Usuario, request.Ruc, cancellationToken))
            return (null, "El usuario o RUC ya se encuentran registrados.");
        var tienda = new Tienda
        {
            Ruc = request.Ruc.Trim(),
            RazonSocial = request.RazonSocial.Trim(),
            Giro = request.Giro.Trim(),
            Usuario = request.Usuario.Trim(),
            PasswordHash = global::BCrypt.Net.BCrypt.HashPassword(request.Password)
        };
        await authRepository.AgregarTiendaAsync(tienda, cancellationToken);
        return (tienda, null);
    }

    public async Task<(Cliente? Cliente, string? Error)> RegistrarClienteAsync(long tiendaId, RegistrarClienteRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Dni) || request.Dni.Trim().Length != 8 ||
            string.IsNullOrWhiteSpace(request.Nombres) || string.IsNullOrWhiteSpace(request.Usuario) ||
            string.IsNullOrWhiteSpace(request.Password))
            return (null, "DNI, nombres, usuario y contraseña son obligatorios; el DNI debe tener 8 caracteres.");
        if (await authRepository.ExisteClienteAsync(tiendaId, request.Usuario, request.Dni, cancellationToken))
            return (null, "El usuario o DNI ya se encuentran registrados en esta tienda.");
        if (request.LimiteCredito < 0 || request.DiaCorte is < 1 or > 28 || request.DiaPago is < 1 or > 28)
            return (null, "Los datos del cliente no cumplen las restricciones requeridas.");
        var cliente = new Cliente
        {
            TiendaId = tiendaId,
            Dni = request.Dni.Trim(),
            NombresCompletos = request.Nombres.Trim(),
            LimiteCredito = decimal.Round(request.LimiteCredito, 2),
            TipoTasa = request.TipoTasa,
            TasaCompensatoria = decimal.Round(request.TasaCompensatoria, 7),
            TasaMoratoria = decimal.Round(request.TasaMoratoria, 7),
            DiaCorte = request.DiaCorte,
            DiaPago = request.DiaPago,
            Usuario = request.Usuario.Trim(),
            PasswordHash = global::BCrypt.Net.BCrypt.HashPassword(request.Password),
            Activo = true
        };
        await authRepository.AgregarClienteAsync(cliente, cancellationToken);
        return (cliente, null);
    }

    public async Task<AuthenticatedUser?> AutenticarAdminAsync(string usuario, string password, CancellationToken cancellationToken)
    {
        var tienda = await authRepository.ObtenerTiendaPorUsuarioAsync(usuario, cancellationToken);
        return tienda is not null && global::BCrypt.Net.BCrypt.Verify(password, tienda.PasswordHash)
            ? new AuthenticatedUser(tienda.Id, tienda.Usuario, "Admin", tienda.Id)
            : null;
    }

    public async Task<AuthenticatedUser?> AutenticarClienteAsync(string usuario, string password, CancellationToken cancellationToken)
    {
        var cliente = await authRepository.ObtenerClientePorUsuarioAsync(usuario, cancellationToken);
        return cliente is not null && cliente.Activo && global::BCrypt.Net.BCrypt.Verify(password, cliente.PasswordHash)
            ? new AuthenticatedUser(cliente.Id, cliente.Usuario, "Cliente", cliente.Id)
            : null;
    }
}
