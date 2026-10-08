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

    public async Task<IReadOnlyList<Cliente>> ListarPorTiendaAsync(long tiendaId, CancellationToken cancellationToken) => await context.Clientes
        .Include(x => x.Compras)
        .Where(x => x.TiendaId == tiendaId)
        .OrderBy(x => x.NombresCompletos)
        .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Pago>> ListarPagosAsync(long clienteId, CancellationToken cancellationToken) => await context.Pagos
        .AsNoTracking()
        .Where(x => x.ClienteId == clienteId)
        .OrderByDescending(x => x.FechaPago)
        .ThenByDescending(x => x.Id)
        .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Cliente>> ListarActivosConDiaCorteAsync(IReadOnlyCollection<int> diasCorte, CancellationToken cancellationToken) => await context.Clientes
        .Include(x => x.Compras).ThenInclude(x => x.Cronogramas)
        .Where(x => x.Activo && diasCorte.Contains(x.DiaCorte))
        .ToListAsync(cancellationToken);

    public Task<bool> ExisteDniAsync(long tiendaId, string dni, long excluirClienteId, CancellationToken cancellationToken) => context.Clientes
        .AnyAsync(x => x.TiendaId == tiendaId && x.Dni == dni && x.Id != excluirClienteId, cancellationToken);
}

public sealed class TiendaRepository(FinvexDbContext context) : ITiendaRepository
{
    public async Task<IReadOnlyList<Tienda>> ListarAsync(CancellationToken cancellationToken) => await context.Tiendas
        .OrderBy(x => x.RazonSocial)
        .ToListAsync(cancellationToken);

    public Task<Tienda?> ObtenerAsync(long id, CancellationToken cancellationToken) => context.Tiendas
        .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
}

public sealed class ProductoRepository(FinvexDbContext context) : IProductoRepository
{
    public async Task<IReadOnlyList<Producto>> ListarPorTiendaAsync(long tiendaId, bool incluirInactivos, CancellationToken cancellationToken) => await context.Productos
        .Where(x => x.TiendaId == tiendaId && (incluirInactivos || x.Activo))
        .OrderBy(x => x.Descripcion)
        .ToListAsync(cancellationToken);

    public Task<Producto?> ObtenerAsync(long id, CancellationToken cancellationToken) => context.Productos
        .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task AgregarAsync(Producto producto, CancellationToken cancellationToken)
    {
        context.Productos.Add(producto);
        return Task.CompletedTask;
    }
}

public sealed class ListadoPagoRepository(FinvexDbContext context) : IListadoPagoRepository
{
    public Task<ListadoPago?> ObtenerAsync(long clienteId, DateTime fechaCorte, CancellationToken cancellationToken) => context.ListadosPago
        .Include(x => x.Items)
        .SingleOrDefaultAsync(x => x.ClienteId == clienteId && x.FechaCorte == fechaCorte, cancellationToken);

    public Task<bool> ExisteAsync(long clienteId, DateTime fechaCorte, CancellationToken cancellationToken) => context.ListadosPago
        .AnyAsync(x => x.ClienteId == clienteId && x.FechaCorte == fechaCorte, cancellationToken);

    public Task AgregarAsync(ListadoPago listado, CancellationToken cancellationToken)
    {
        context.ListadosPago.Add(listado);
        return Task.CompletedTask;
    }
}

public sealed class UnitOfWork(FinvexDbContext context, AuditoriaService auditoria) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (!auditoria.TienePendientes) return await context.SaveChangesAsync(cancellationToken);
        await using var transaccion = await context.Database.BeginTransactionAsync(cancellationToken);
        var cambios = await context.SaveChangesAsync(cancellationToken);
        auditoria.ResolverPendientes();
        cambios += await context.SaveChangesAsync(cancellationToken);
        await transaccion.CommitAsync(cancellationToken);
        return cambios;
    }
}
