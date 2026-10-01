using Finvex.Domain;

namespace Finvex.Tests;

internal static class Formulas
{
    public static decimal R2(decimal valor) => decimal.Round(valor, 2, MidpointRounding.AwayFromZero);

    public static decimal R7(decimal valor) => decimal.Round(valor, 7, MidpointRounding.AwayFromZero);

    public static decimal Pow(decimal baseValor, double exponente) => (decimal)Math.Pow((double)baseValor, exponente);

    public static decimal Tem(TipoTasa tipo, decimal tasa) => tipo == TipoTasa.Nominal
        ? R7(tasa / 360m * 30m)
        : R7(Pow(1m + tasa, 30d / 360d) - 1m);

    public static decimal Ted(decimal tem) => R7(Pow(1m + tem, 1d / 30d) - 1m);

    public static decimal Interes(decimal saldo, decimal ted, int dias) => dias <= 0 ? 0m : R2(saldo * (Pow(1m + ted, dias) - 1m));

    public static decimal CuotaFrancesa(decimal capital, decimal tem, int n)
    {
        var factor = Pow(1m + tem, n);
        return R2(capital * tem * factor / (factor - 1m));
    }
}

internal static class Escenarios
{
    public static Cliente ClienteJuego1() => new()
    {
        Id = 1,
        TiendaId = 10,
        LimiteCredito = 2000m,
        TipoTasa = TipoTasa.Efectiva,
        TasaCompensatoria = 0.60m,
        TasaMoratoria = 0.80m,
        DiaCorte = 20,
        DiaPago = 26,
        MaxMeses = 6,
        HoraCorte = new TimeSpan(23, 59, 59)
    };

    public static Cliente ClienteJuego2() => new()
    {
        Id = 2,
        TiendaId = 10,
        LimiteCredito = 500m,
        TipoTasa = TipoTasa.Nominal,
        TasaCompensatoria = 0.36m,
        TasaMoratoria = 0.48m,
        DiaCorte = 25,
        DiaPago = 5,
        MaxMeses = 1,
        HoraCorte = new TimeSpan(23, 59, 59)
    };

    public static Compra AgregarCompra(Cliente cliente, Finvex.Application.IFinancialEngineService motor, long id, string producto,
        decimal precio, ModalidadCompra modalidad, int plazo, DateTime fecha)
    {
        var compra = new Compra
        {
            Id = id,
            ClienteId = cliente.Id,
            Cliente = cliente,
            Producto = producto,
            PrecioCredito = precio,
            SaldoCapital = precio,
            Modalidad = modalidad,
            PlazoMeses = modalidad == ModalidadCompra.Cuotas ? plazo : 1,
            FechaCompra = fecha
        };
        if (modalidad == ModalidadCompra.Cuotas)
        {
            foreach (var cuota in motor.GenerarCronograma(compra, cliente)) compra.Cronogramas.Add(cuota);
            compra.SaldoCapital = compra.Cronogramas.Sum(x => x.Amortizacion);
        }
        cliente.Compras.Add(compra);
        return compra;
    }
}
