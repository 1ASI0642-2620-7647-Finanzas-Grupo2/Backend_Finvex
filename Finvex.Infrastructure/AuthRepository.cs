using Finvex.Application;
using Finvex.Domain;
using Microsoft.EntityFrameworkCore;

namespace Finvex.Infrastructure;

public sealed class AuthRepository(FinvexDbContext context) : IAuthRepository
{
    public Task<Tienda?> ObtenerTiendaPorUsuarioAsync(string usuario, CancellationToken cancellationToken) => context.Tiendas
        .SingleOrDefaultAsync(x => x.Usuario == usuario, cancellationToken);

    public Task<Cliente?> ObtenerClientePorUsuarioAsync(string usuario, CancellationToken cancellationToken) => context.Clientes
        .SingleOrDefaultAsync(x => x.Usuario == usuario, cancellationToken);

    public Task<bool> ExisteTiendaAsync(string usuario, string ruc, CancellationToken cancellationToken) => context.Tiendas
        .AnyAsync(x => x.Usuario == usuario || x.Ruc == ruc, cancellationToken);

    public Task<bool> ExisteClienteAsync(long tiendaId, string usuario, string dni, CancellationToken cancellationToken) => context.Clientes
        .AnyAsync(x => x.TiendaId == tiendaId && (x.Usuario == usuario || x.Dni == dni), cancellationToken);

    public Task AgregarTiendaAsync(Tienda tienda, CancellationToken cancellationToken)
    {
        context.Tiendas.Add(tienda);
        return Task.CompletedTask;
    }

    public Task AgregarClienteAsync(Cliente cliente, CancellationToken cancellationToken)
    {
        context.Clientes.Add(cliente);
        return Task.CompletedTask;
    }

    public Task<AdministradorSistema?> ObtenerAdministradorSistemaPorUsuarioAsync(string usuario, CancellationToken cancellationToken) => context.AdministradoresSistema
        .SingleOrDefaultAsync(x => x.Usuario == usuario, cancellationToken);

    public Task AgregarAdministradorSistemaAsync(AdministradorSistema administrador, CancellationToken cancellationToken)
    {
        context.AdministradoresSistema.Add(administrador);
        return Task.CompletedTask;
    }
}
