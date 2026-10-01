using Finvex.Application;
using Finvex.Domain;

namespace Finvex.Tests;

public sealed class JuegosDePruebaTests
{
    private readonly FinancialEngineService motor = new();

    [Fact]
    public void Juego1_CuotasConGraciaYMora()
    {
        var cliente = Escenarios.ClienteJuego1();
        var compra = Escenarios.AgregarCompra(cliente, motor, 1, "Refrigeradora", 900m, ModalidadCompra.Cuotas, 3, new DateTime(2026, 9, 15, 10, 30, 0));

        Assert.Equal(0.0399441m, motor.ConvertirATem(cliente.TipoTasa, cliente.TasaCompensatoria));
        Assert.Equal(0.0013064m, motor.CalcularTedCompensatoria(cliente));
        Assert.Equal(0.0016341m, motor.CalcularTedMoratoria(cliente));
        Assert.Equal(11, motor.CalcularDiasGracia(compra, cliente));

        var cuotas = compra.Cronogramas.OrderBy(x => x.NroCuota)
            .Select(x => (x.FechaVencimiento, x.SaldoInicial, x.Interes, x.Amortizacion, x.CuotaFija))
            .ToArray();
        Assert.Equal(new[]
        {
            (new DateTime(2026, 10, 26), 913.02m, 36.47m, 292.50m, 328.97m),
            (new DateTime(2026, 11, 26), 620.52m, 24.79m, 304.18m, 328.97m),
            (new DateTime(2026, 12, 26), 316.34m, 12.64m, 316.34m, 328.98m)
        }, cuotas);
        Assert.Equal(913.02m, compra.SaldoCapital);

        var pago = motor.CalcularPago(cliente, 331.67m, new DateTime(2026, 10, 31));
        Assert.Equal((331.67m, 2.70m, 36.47m, 292.50m), (pago.Monto, pago.ImputacionMora, pago.ImputacionInteres, pago.ImputacionCapital));
    }

    [Fact]
    public void Juego2_FinDeMesConCompraPosteriorAlCorte()
    {
        var cliente = Escenarios.ClienteJuego2();
        Escenarios.AgregarCompra(cliente, motor, 1, "Arroz 50 kg", 150m, ModalidadCompra.FinDeMes, 1, new DateTime(2026, 3, 10, 9, 0, 0));
        Escenarios.AgregarCompra(cliente, motor, 2, "Aceite caja", 120m, ModalidadCompra.FinDeMes, 1, new DateTime(2026, 3, 25, 23, 0, 0));
        Escenarios.AgregarCompra(cliente, motor, 3, "Azúcar 10 kg", 80m, ModalidadCompra.FinDeMes, 1, new DateTime(2026, 3, 26, 8, 0, 0));

        Assert.Equal(0.0300000m, motor.ConvertirATem(cliente.TipoTasa, cliente.TasaCompensatoria));
        Assert.Equal(0.0009858m, motor.CalcularTedCompensatoria(cliente));
        Assert.Equal(0.0013082m, motor.CalcularTedMoratoria(cliente));

        var aTiempo = motor.CalcularExigible(cliente, new DateTime(2026, 4, 5));
        Assert.Equal(new[] { (150m, 3.89m, 26), (120m, 1.31m, 11) },
            aTiempo.Obligaciones.Select(x => (x.Capital, x.Interes, x.DiasInteres)).ToArray());
        Assert.Equal(275.20m, aTiempo.Total);

        var conMora = motor.CalcularExigible(cliente, new DateTime(2026, 4, 12));
        Assert.Equal(new[] { 1.41m, 1.12m }, conMora.Obligaciones.Select(x => x.Mora).ToArray());
        Assert.Equal(277.73m, conMora.Total);
        var pago = motor.CalcularPago(cliente, 277.73m, new DateTime(2026, 4, 12));
        Assert.Equal((2.53m, 5.20m, 270.00m), (pago.ImputacionMora, pago.ImputacionInteres, pago.ImputacionCapital));

        var siguiente = motor.CalcularExigible(cliente, new DateTime(2026, 5, 5)).Obligaciones.Single(x => x.Compra.Id == 3);
        Assert.Equal((new DateTime(2026, 5, 5), 40, 3.22m), (siguiente.FechaVencimiento, siguiente.DiasInteres, siguiente.Interes));

        var listado = motor.CalcularListadoPago(cliente, new DateTime(2026, 3, 25), new DateTime(2026, 4, 12, 10, 0, 0));
        Assert.Equal(new[] { TipoItemListado.Compra, TipoItemListado.Compra, TipoItemListado.InteresMora }, listado.Items.Select(x => x.Tipo).ToArray());
        Assert.Equal("Intereses por mora", listado.Items.Last().Descripcion);
        Assert.Equal(277.73m, listado.Total);
    }
}
