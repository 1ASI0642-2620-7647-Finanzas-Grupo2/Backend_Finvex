using Finvex.Domain;

namespace Finvex.Application;

public sealed class ListadoPagoService(
    IFinancialEngineService financialEngine,
    IListadoPagoRepository listados,
    IClienteRepository clientes) : IListadoPagoService
{
    public DateTime ResolverFechaCorte(Cliente cliente, DateTime? fechaCorte, DateTime ahora) => fechaCorte.HasValue
        ? financialEngine.ObtenerFechaCorteCiclo(fechaCorte.Value.Date, cliente.DiaCorte, cliente.HoraCorte)
        : financialEngine.ObtenerUltimoCorteCerrado(ahora, cliente.DiaCorte, cliente.HoraCorte);

    public ListadoPagoResponse Calcular(Cliente cliente, DateTime? fechaCorte, DateTime ahora) =>
        financialEngine.CalcularListadoPago(cliente, ResolverFechaCorte(cliente, fechaCorte, ahora), ahora);

    public async Task<(ListadoPago? Listado, bool Creado, string? Error)> GenerarAsync(Cliente cliente, DateTime? fechaCorte, DateTime ahora, CancellationToken cancellationToken)
    {
        var corte = ResolverFechaCorte(cliente, fechaCorte, ahora);
        if (corte > ahora) return (null, false, "El ciclo aún no ha cerrado; el listado se genera después de la fecha y hora de corte.");
        var existente = await listados.ObtenerAsync(cliente.Id, corte.Date, cancellationToken);
        if (existente is not null) return (existente, false, null);
        var listado = CrearEntidad(financialEngine.CalcularListadoPago(cliente, corte, ahora));
        await listados.AgregarAsync(listado, cancellationToken);
        return (listado, true, null);
    }

    public async Task<int> GenerarUltimosCortesPendientesAsync(DateTime ahora, CancellationToken cancellationToken)
    {
        var generados = 0;
        foreach (var cliente in await clientes.ListarActivosConDiaCorteAsync(Enumerable.Range(1, 28).ToArray(), cancellationToken))
        {
            var corte = financialEngine.ObtenerUltimoCorteCerrado(ahora, cliente.DiaCorte, cliente.HoraCorte);
            if (await listados.ExisteAsync(cliente.Id, corte.Date, cancellationToken)) continue;
            await listados.AgregarAsync(CrearEntidad(financialEngine.CalcularListadoPago(cliente, corte, ahora)), cancellationToken);
            generados++;
        }
        return generados;
    }

    public ListadoPagoResponse CrearRespuesta(ListadoPago listado) => new(
        listado.ClienteId, listado.FechaCorte, listado.FechaPago, listado.FechaCalculo, listado.Total,
        listado.Items.OrderBy(x => x.Orden)
            .Select(x => new ItemListadoPagoResponse(x.Tipo, x.CompraId, x.NroCuota, x.Descripcion, x.Fecha, x.Capital, x.Dias, x.InteresCompensatorio, x.Monto))
            .ToArray(),
        listado.Id, listado.FechaGeneracionUtc);

    private static ListadoPago CrearEntidad(ListadoPagoResponse calculado) => new()
    {
        ClienteId = calculado.ClienteId,
        FechaCorte = calculado.FechaCorte,
        FechaPago = calculado.FechaPago,
        FechaCalculo = calculado.FechaCalculo,
        Total = calculado.Total,
        FechaGeneracionUtc = DateTime.UtcNow,
        Items = calculado.Items.Select((x, indice) => new ItemListadoPago
        {
            Orden = indice + 1,
            Tipo = x.Tipo,
            CompraId = x.CompraId,
            NroCuota = x.NroCuota,
            Descripcion = x.Descripcion,
            Fecha = x.Fecha,
            Capital = x.Capital,
            Dias = x.Dias,
            InteresCompensatorio = x.InteresCompensatorio,
            Monto = x.Monto
        }).ToList()
    };
}
