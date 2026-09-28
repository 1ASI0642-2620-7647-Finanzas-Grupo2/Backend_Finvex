using Finvex.Application;
using Finvex.Domain;
using Microsoft.EntityFrameworkCore;

namespace Finvex.Infrastructure;

public sealed class ClienteRepository(FinvexDbContext context) : IClienteRepository
{
    public Task<Cliente?> ObtenerConComprasAsync(long id, CancellationToken cancellationToken) => context.Clientes
        .Include(x => x.Compras).ThenInclude(x => x.Cronogramas)
        .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task GuardarAsync(Cliente cliente, CancellationToken cancellationToken)
    {
        if (context.Entry(cliente).State == EntityState.Detached) context.Clientes.Update(cliente);
        return Task.CompletedTask;
    }
}

public sealed class UnitOfWork(FinvexDbContext context) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => context.SaveChangesAsync(cancellationToken);
}
