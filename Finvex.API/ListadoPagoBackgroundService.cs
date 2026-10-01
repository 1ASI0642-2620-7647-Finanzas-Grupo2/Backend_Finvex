using Finvex.Application;

namespace Finvex.API;

public sealed class ListadoPagoBackgroundService(IServiceScopeFactory scopeFactory, ILogger<ListadoPagoBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var temporizador = new PeriodicTimer(Intervalo);
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var servicio = scope.ServiceProvider.GetRequiredService<IListadoPagoService>();
                var ahora = HoraLima.Ahora;
                var generados = await servicio.GenerarCortesDelDiaAsync(ahora, stoppingToken);
                if (generados > 0)
                {
                    await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(stoppingToken);
                    logger.LogInformation("Se generaron {Cantidad} listados de pago del corte {Fecha:dd/MM/yyyy}.", generados, ahora);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error al generar los listados de pago del día.");
            }
        }
        while (await temporizador.WaitForNextTickAsync(stoppingToken));
    }
}
