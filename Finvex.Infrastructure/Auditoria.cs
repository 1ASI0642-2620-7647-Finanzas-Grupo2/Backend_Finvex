using Finvex.Application;
using Finvex.Domain;
using Microsoft.EntityFrameworkCore;

namespace Finvex.Infrastructure;

public sealed class AuditoriaService(FinvexDbContext context) : IAuditoriaService
{
    private const int LongitudMaximaDetalle = 1000;
    private readonly List<(Operacion Operacion, Action<Operacion> Completar)> pendientes = new();

    public bool TienePendientes => pendientes.Count > 0;

    public void Registrar(Operacion operacion, Action<Operacion>? completarAlGuardar = null)
    {
        if (operacion.FechaUtc == default) operacion.FechaUtc = DateTime.UtcNow;
        if (operacion.Detalle is { Length: > LongitudMaximaDetalle }) operacion.Detalle = operacion.Detalle[..LongitudMaximaDetalle];
        context.Operaciones.Add(operacion);
        if (completarAlGuardar is not null) pendientes.Add((operacion, completarAlGuardar));
    }

    public void ResolverPendientes()
    {
        foreach (var (operacion, completar) in pendientes) completar(operacion);
        pendientes.Clear();
    }
}

public sealed class AuditoriaRepository(FinvexDbContext context) : IAuditoriaRepository
{
    public async Task<(IReadOnlyList<Operacion> Items, int Total)> ListarAsync(long? tiendaId, DateTime? desdeUtc, DateTime? hastaUtc,
        string? accion, int pagina, int tamanoPagina, CancellationToken cancellationToken)
    {
        var consulta = context.Operaciones.AsNoTracking();
        if (tiendaId.HasValue) consulta = consulta.Where(x => x.TiendaId == tiendaId);
        if (desdeUtc.HasValue) consulta = consulta.Where(x => x.FechaUtc >= desdeUtc);
        if (hastaUtc.HasValue) consulta = consulta.Where(x => x.FechaUtc < hastaUtc);
        if (!string.IsNullOrWhiteSpace(accion)) consulta = consulta.Where(x => x.Accion == accion);
        var total = await consulta.CountAsync(cancellationToken);
        var items = await consulta
            .OrderByDescending(x => x.FechaUtc)
            .ThenByDescending(x => x.Id)
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .ToListAsync(cancellationToken);
        return (items, total);
    }
}
