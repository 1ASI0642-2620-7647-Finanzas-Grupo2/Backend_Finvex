using BCrypt.Net;
using Finvex.Domain;

namespace Finvex.Application;

public sealed record LoginRequest(string Usuario, string Password, string? TiendaRuc = null);
public sealed record AuthenticatedUser(long Id, string Usuario, string Rol, long ContextId, long? TiendaId = null);
public sealed record LoginResponse(string Token, string Rol, long Id, long? TiendaId, long? ClienteId, DateTime ExpiraEn);

public interface IAuthRepository
{
    Task<Tienda?> ObtenerTiendaPorUsuarioAsync(string usuario, CancellationToken cancellationToken);
    Task<Cliente?> ObtenerClientePorUsuarioAsync(string usuario, string tiendaRuc, CancellationToken cancellationToken);
    Task<bool> ExisteTiendaAsync(string usuario, string ruc, CancellationToken cancellationToken);
    Task<bool> ExisteClienteAsync(long tiendaId, string usuario, string dni, CancellationToken cancellationToken);
    Task AgregarTiendaAsync(Tienda tienda, CancellationToken cancellationToken);
    Task AgregarClienteAsync(Cliente cliente, CancellationToken cancellationToken);
    Task<AdministradorSistema?> ObtenerAdministradorSistemaPorUsuarioAsync(string usuario, CancellationToken cancellationToken);
    Task AgregarAdministradorSistemaAsync(AdministradorSistema administrador, CancellationToken cancellationToken);
}

public interface IAuthService
{
    Task<(Tienda? Tienda, string? Error)> RegistrarTiendaAsync(RegistrarAdminRequest request, CancellationToken cancellationToken);
    Task<(Cliente? Cliente, string? Error)> RegistrarClienteAsync(long tiendaId, RegistrarClienteRequest request, CancellationToken cancellationToken);
    Task<AuthenticatedUser?> AutenticarAdminAsync(string usuario, string password, CancellationToken cancellationToken);
    Task<AuthenticatedUser?> AutenticarClienteAsync(string usuario, string password, string? tiendaRuc, CancellationToken cancellationToken);
    Task<AuthenticatedUser?> AutenticarAdminSistemaAsync(string usuario, string password, CancellationToken cancellationToken);
    Task<bool> SembrarAdministradorSistemaAsync(string usuario, string password, CancellationToken cancellationToken);
}

public sealed class AuthService(IAuthRepository authRepository) : IAuthService
{
    public async Task<(Tienda? Tienda, string? Error)> RegistrarTiendaAsync(RegistrarAdminRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Ruc) || !request.Ruc.Trim().All(char.IsDigit) || request.Ruc.Trim().Length != 11 ||
            string.IsNullOrWhiteSpace(request.RazonSocial) || string.IsNullOrWhiteSpace(request.Giro) ||
            string.IsNullOrWhiteSpace(request.Usuario) || string.IsNullOrWhiteSpace(request.Password))
            return (null, "RUC, razón social, giro, usuario y contraseña son obligatorios; el RUC debe tener 11 dígitos.");
        if (request.Password.Length < 6) return (null, "La contraseña debe tener al menos 6 caracteres.");
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
        if (string.IsNullOrWhiteSpace(request.Dni) || !request.Dni.Trim().All(char.IsDigit) || request.Dni.Trim().Length != 8 ||
            string.IsNullOrWhiteSpace(request.Nombres) || string.IsNullOrWhiteSpace(request.Usuario) ||
            string.IsNullOrWhiteSpace(request.Password))
            return (null, "DNI, nombres, usuario y contraseña son obligatorios; el DNI debe tener 8 dígitos.");
        if (request.Password.Length < 6) return (null, "La contraseña debe tener al menos 6 caracteres.");
        if (await authRepository.ExisteClienteAsync(tiendaId, request.Usuario, request.Dni, cancellationToken))
            return (null, "El usuario o DNI ya se encuentran registrados en esta tienda.");
        var horaCorte = request.HoraCorte ?? new TimeSpan(23, 59, 59);
        var error = ValidacionesCliente.ValidarCondiciones(request.LimiteCredito, request.TasaCompensatoria, request.TasaMoratoria,
            request.DiaCorte, request.DiaPago, request.MaxMeses, horaCorte);
        if (error is not null) return (null, error);
        var cliente = new Cliente
        {
            TiendaId = tiendaId,
            Dni = request.Dni.Trim(),
            NombresCompletos = request.Nombres.Trim(),
            LimiteCredito = decimal.Round(request.LimiteCredito, 2, MidpointRounding.AwayFromZero),
            TipoTasa = request.TipoTasa,
            TasaCompensatoria = decimal.Round(request.TasaCompensatoria, 7, MidpointRounding.AwayFromZero),
            TasaMoratoria = decimal.Round(request.TasaMoratoria, 7, MidpointRounding.AwayFromZero),
            DiaCorte = request.DiaCorte,
            DiaPago = request.DiaPago,
            Moneda = request.Moneda,
            MaxMeses = request.MaxMeses,
            HoraCorte = horaCorte,
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
        return tienda is not null && tienda.Activo && global::BCrypt.Net.BCrypt.Verify(password, tienda.PasswordHash)
            ? new AuthenticatedUser(tienda.Id, tienda.Usuario, "Admin", tienda.Id, tienda.Id)
            : null;
    }

    public async Task<AuthenticatedUser?> AutenticarClienteAsync(string usuario, string password, string? tiendaRuc, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tiendaRuc) || tiendaRuc.Trim().Length != 11 || !tiendaRuc.Trim().All(char.IsDigit)) return null;
        var cliente = await authRepository.ObtenerClientePorUsuarioAsync(usuario.Trim(), tiendaRuc.Trim(), cancellationToken);
        return cliente is not null && cliente.Activo && global::BCrypt.Net.BCrypt.Verify(password, cliente.PasswordHash)
            ? new AuthenticatedUser(cliente.Id, cliente.Usuario, "Cliente", cliente.Id, cliente.TiendaId)
            : null;
    }

    public async Task<AuthenticatedUser?> AutenticarAdminSistemaAsync(string usuario, string password, CancellationToken cancellationToken)
    {
        var administrador = await authRepository.ObtenerAdministradorSistemaPorUsuarioAsync(usuario, cancellationToken);
        return administrador is not null && administrador.Activo && global::BCrypt.Net.BCrypt.Verify(password, administrador.PasswordHash)
            ? new AuthenticatedUser(administrador.Id, administrador.Usuario, "AdminSistema", administrador.Id)
            : null;
    }

    public async Task<bool> SembrarAdministradorSistemaAsync(string usuario, string password, CancellationToken cancellationToken)
    {
        if (await authRepository.ObtenerAdministradorSistemaPorUsuarioAsync(usuario.Trim(), cancellationToken) is not null) return false;
        await authRepository.AgregarAdministradorSistemaAsync(new AdministradorSistema
        {
            Usuario = usuario.Trim(),
            PasswordHash = global::BCrypt.Net.BCrypt.HashPassword(password),
            Activo = true
        }, cancellationToken);
        return true;
    }
}
