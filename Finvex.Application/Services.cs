using Finvex.Domain;

namespace Finvex.Application;

public sealed class FinancialEngineService : IFinancialEngineService
{
    private const int DiasAnioComercial = 360;
    private const int DiasMesComercial = 30;

    public decimal ConvertirATem(TipoTasa tipoTasa, decimal tasa)
    {
        var valor = tipoTasa == TipoTasa.Nominal
            ? tasa / DiasAnioComercial * DiasMesComercial
            : (decimal)(Math.Pow((double)(1m + tasa), (double)DiasMesComercial / DiasAnioComercial) - 1d);
        return decimal.Round(valor, 7, MidpointRounding.AwayFromZero);
    }

    public decimal CalcularTed(decimal tem) => decimal.Round(
        (decimal)(Math.Pow((double)(1m + tem), 1d / DiasMesComercial) - 1d), 7, MidpointRounding.AwayFromZero);

    public decimal CalcularCuotaFrancesa(decimal capital, decimal tem, int plazoMeses)
    {
        if (plazoMeses <= 0) throw new DomainException("El plazo debe ser mayor que cero.");
        if (tem == 0) return decimal.Round(capital / plazoMeses, 2);
        var factor = (decimal)Math.Pow((double)(1m + tem), plazoMeses);
        return decimal.Round(capital * tem * factor / (factor - 1m), 2, MidpointRounding.AwayFromZero);
    }

    public IReadOnlyCollection<Cronograma> GenerarCronograma(Compra compra, Cliente cliente)
    {
        var tem = ConvertirATem(cliente.TipoTasa, cliente.TasaCompensatoria);
        var ted = CalcularTed(tem);
        var fechaCorte = ObtenerFechaCorte(compra.FechaCompra, cliente.DiaCorte);
        var diasGracia = compra.FechaCompra < fechaCorte ? (fechaCorte - compra.FechaCompra).Days : 0;
        var capitalAjustado = compra.PrecioCredito * (decimal)Math.Pow((double)(1m + ted), diasGracia);
        var cuota = CalcularCuotaFrancesa(capitalAjustado, tem, compra.PlazoMeses);
        var saldo = capitalAjustado;
        var cuotas = new List<Cronograma>();

        for (var numero = 1; numero <= compra.PlazoMeses; numero++)
        {
            var interes = decimal.Round(saldo * tem, 2, MidpointRounding.AwayFromZero);
            var amortizacion = numero == compra.PlazoMeses ? saldo : decimal.Round(cuota - interes, 2, MidpointRounding.AwayFromZero);
            var cuotaReal = interes + amortizacion;
            cuotas.Add(new Cronograma
            {
                Compra = compra,
                NroCuota = numero,
                FechaVencimiento = fechaCorte.AddMonths(numero),
                SaldoInicial = decimal.Round(saldo, 2),
                Interes = interes,
                Amortizacion = amortizacion,
                CuotaFija = cuotaReal,
                Estado = EstadoCronograma.Pendiente
            });
            saldo -= amortizacion;
        }
        return cuotas;
    }

    public decimal CalcularInteresDiario(decimal saldo, decimal ted, int dias) => dias <= 0
        ? 0m
        : decimal.Round(saldo * ((decimal)Math.Pow((double)(1m + ted), dias) - 1m), 2, MidpointRounding.AwayFromZero);

    public (decimal Mora, decimal Interes, decimal Capital) AplicarPrelacion(decimal monto, decimal mora, decimal interes, decimal capital)
    {
        var imputacionMora = Math.Min(monto, Math.Max(0m, mora));
        var restante = monto - imputacionMora;
        var imputacionInteres = Math.Min(restante, Math.Max(0m, interes));
        restante -= imputacionInteres;
        var imputacionCapital = Math.Min(restante, Math.Max(0m, capital));
        return (imputacionMora, imputacionInteres, imputacionCapital);
    }

    private static DateTime ObtenerFechaCorte(DateTime fecha, int diaCorte)
    {
        var dia = Math.Min(diaCorte, DateTime.DaysInMonth(fecha.Year, fecha.Month));
        var corte = new DateTime(fecha.Year, fecha.Month, dia);
        return fecha <= corte ? corte : corte.AddMonths(1);
    }
}
