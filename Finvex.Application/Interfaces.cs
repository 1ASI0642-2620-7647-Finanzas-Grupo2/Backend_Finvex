using Finvex.Domain;

namespace Finvex.Application;

public interface IFinancialEngineService
{
    decimal ConvertirATem(TipoTasa tipoTasa, decimal tasa);
    decimal CalcularTed(decimal tem);
    decimal CalcularCuotaFrancesa(decimal capital, decimal tem, int plazoMeses);
    IReadOnlyCollection<Cronograma> GenerarCronograma(Compra compra, Cliente cliente);
    (decimal Mora, decimal Interes, decimal Capital) AplicarPrelacion(decimal monto, decimal mora, decimal interes, decimal capital);
    decimal CalcularInteresDiario(decimal saldo, decimal ted, int dias);
}

public interface IClienteRepository
{
    Task<Cliente?> ObtenerConComprasAsync(long id, CancellationToken cancellationToken);
    Task GuardarAsync(Cliente cliente, CancellationToken cancellationToken);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
