using Finvex.Application;
using Finvex.Domain;
using static Finvex.Tests.Formulas;

namespace Finvex.Tests;

public sealed class FinancialEngineServiceTests
{
    private readonly FinancialEngineService motor = new();

    [Theory]
    [InlineData(TipoTasa.Nominal, 0.36)]
    [InlineData(TipoTasa.Nominal, 0.48)]
    [InlineData(TipoTasa.Efectiva, 0.60)]
    [InlineData(TipoTasa.Efectiva, 0.80)]
    public void ConvertirATem_AplicaLaFormulaSegunElTipoDeTasa(TipoTasa tipo, double tasa)
    {
        var valor = (decimal)tasa;
        var esperado = tipo == TipoTasa.Nominal
            ? R7(valor / 360m * 30m)
            : R7((decimal)Math.Pow(1d + tasa, 30d / 360d) - 1m);

        Assert.Equal(esperado, motor.ConvertirATem(tipo, valor));
    }

    [Fact]
    public void ConvertirATem_ValoresConocidos()
    {
        Assert.Equal(0.0300000m, motor.ConvertirATem(TipoTasa.Nominal, 0.36m));
        Assert.Equal(0.0399441m, motor.ConvertirATem(TipoTasa.Efectiva, 0.60m));
    }

    [Theory]
    [InlineData(0.03)]
    [InlineData(0.0399441)]
    [InlineData(0.0502017)]
    public void CalcularTed_EsLaRaizTreintavaDeLaTem(double tem)
    {
        var esperado = R7((decimal)Math.Pow(1d + tem, 1d / 30d) - 1m);

        Assert.Equal(esperado, motor.CalcularTed((decimal)tem));
    }

    [Fact]
    public void CalcularTed_CapitalizadaTreintaDiasDevuelveLaTem()
    {
        var tem = 0.03m;
        var ted = motor.CalcularTed(tem);

        Assert.Equal(tem, R7(Pow(1m + ted, 30) - 1m), 5);
    }

    [Theory]
    [InlineData(913.02, 0.0399441, 3)]
    [InlineData(1000, 0.03, 12)]
    [InlineData(500, 0.0502017, 6)]
    public void CalcularCuotaFrancesa_AplicaLaFormulaDeAnualidadVencida(double capital, double tem, int plazo)
    {
        var esperado = CuotaFrancesa((decimal)capital, (decimal)tem, plazo);

        Assert.Equal(esperado, motor.CalcularCuotaFrancesa((decimal)capital, (decimal)tem, plazo));
    }

    [Fact]
    public void CalcularCuotaFrancesa_SinTasaDivideElCapital()
    {
        Assert.Equal(333.33m, motor.CalcularCuotaFrancesa(1000m, 0m, 3));
    }

    [Fact]
    public void GenerarCronograma_EjemploDelEnunciado()
    {
        var cliente = Escenarios.ClienteJuego1();
        var compra = Escenarios.AgregarCompra(cliente, motor, 1, "Refrigeradora", 900m, ModalidadCompra.Cuotas, 3, new DateTime(2026, 9, 15, 10, 30, 0));

        Assert.Equal(11, motor.CalcularDiasGracia(compra, cliente));
        Assert.Equal(new DateTime(2026, 9, 26), motor.ObtenerFechaPagoCompra(compra, cliente));
        Assert.Equal(
            new[] { new DateTime(2026, 10, 26), new DateTime(2026, 11, 26), new DateTime(2026, 12, 26) },
            compra.Cronogramas.OrderBy(x => x.NroCuota).Select(x => x.FechaVencimiento).ToArray());
    }

    [Fact]
    public void GenerarCronograma_CapitalizaLaGraciaYAmortizaTodoElCapital()
    {
        var cliente = Escenarios.ClienteJuego1();
        var compra = Escenarios.AgregarCompra(cliente, motor, 1, "Refrigeradora", 900m, ModalidadCompra.Cuotas, 3, new DateTime(2026, 9, 15, 10, 30, 0));
        var tem = Tem(TipoTasa.Efectiva, 0.60m);
        var ted = Ted(tem);
        var capitalizado = R2(900m * Pow(1m + ted, 11));
        var cuota = CuotaFrancesa(capitalizado, tem, 3);
        var cuotas = compra.Cronogramas.OrderBy(x => x.NroCuota).ToArray();

        Assert.Equal(capitalizado, cuotas[0].SaldoInicial);
        Assert.Equal(capitalizado, cuotas.Sum(x => x.Amortizacion));
        Assert.Equal(R2(capitalizado * tem), cuotas[0].Interes);
        Assert.All(cuotas.Take(2), x => Assert.Equal(cuota, x.CuotaFija));
        Assert.True(Math.Abs(cuotas[2].CuotaFija - cuota) <= 0.02m);
    }

    [Fact]
    public void CompraPosteriorAlCorte_PerteneceAlCicloSiguiente()
    {
        var cliente = Escenarios.ClienteJuego2();

        var antesDeLaHora = motor.ObtenerFechaCorteCiclo(new DateTime(2026, 3, 25, 23, 0, 0), cliente.DiaCorte, cliente.HoraCorte);
        var despuesDelCorte = motor.ObtenerFechaCorteCiclo(new DateTime(2026, 3, 26, 8, 0, 0), cliente.DiaCorte, cliente.HoraCorte);
        var horaCorteTemprana = motor.ObtenerFechaCorteCiclo(new DateTime(2026, 3, 25, 23, 0, 0), cliente.DiaCorte, new TimeSpan(18, 0, 0));

        Assert.Equal(new DateTime(2026, 3, 25, 23, 59, 59), antesDeLaHora);
        Assert.Equal(new DateTime(2026, 4, 25, 23, 59, 59), despuesDelCorte);
        Assert.Equal(new DateTime(2026, 4, 25, 18, 0, 0), horaCorteTemprana);
        Assert.Equal(new DateTime(2026, 4, 5), motor.ObtenerFechaPagoCiclo(antesDeLaHora, cliente.DiaCorte, cliente.DiaPago));
        Assert.Equal(new DateTime(2026, 5, 5), motor.ObtenerFechaPagoCiclo(despuesDelCorte, cliente.DiaCorte, cliente.DiaPago));
    }

    [Fact]
    public void FinDeMesSinMora_ExigibleEnLaFechaDePago()
    {
        var cliente = Escenarios.ClienteJuego2();
        Escenarios.AgregarCompra(cliente, motor, 1, "Arroz 50 kg", 150m, ModalidadCompra.FinDeMes, 1, new DateTime(2026, 3, 10, 9, 0, 0));
        Escenarios.AgregarCompra(cliente, motor, 2, "Aceite caja", 120m, ModalidadCompra.FinDeMes, 1, new DateTime(2026, 3, 25, 23, 0, 0));
        var ted = Ted(Tem(TipoTasa.Nominal, 0.36m));
        var esperado = 150m + Interes(150m, ted, 26) + 120m + Interes(120m, ted, 11);

        Assert.Equal(0m, motor.CalcularExigible(cliente, new DateTime(2026, 4, 4)).Total);
        var exigible = motor.CalcularExigible(cliente, new DateTime(2026, 4, 5));
        Assert.Equal(esperado, exigible.Total);
        Assert.Equal(0m, exigible.Mora);
        Assert.Equal(new[] { 26, 11 }, exigible.Obligaciones.Select(x => x.DiasInteres).ToArray());

        var pago = motor.CalcularPago(cliente, esperado, new DateTime(2026, 4, 5));
        Assert.Equal((0m, exigible.Interes, 270m), (pago.ImputacionMora, pago.ImputacionInteres, pago.ImputacionCapital));
    }

    [Fact]
    public void FinDeMesConMora_CalculaMoraSobreElTotalVencido()
    {
        var cliente = Escenarios.ClienteJuego2();
        Escenarios.AgregarCompra(cliente, motor, 1, "Arroz 50 kg", 150m, ModalidadCompra.FinDeMes, 1, new DateTime(2026, 3, 10, 9, 0, 0));
        Escenarios.AgregarCompra(cliente, motor, 2, "Aceite caja", 120m, ModalidadCompra.FinDeMes, 1, new DateTime(2026, 3, 25, 23, 0, 0));
        var ted = Ted(Tem(TipoTasa.Nominal, 0.36m));
        var tedMora = Ted(Tem(TipoTasa.Nominal, 0.48m));
        var base1 = 150m + Interes(150m, ted, 26);
        var base2 = 120m + Interes(120m, ted, 11);
        var mora = Interes(base1, tedMora, 7) + Interes(base2, tedMora, 7);

        var exigible = motor.CalcularExigible(cliente, new DateTime(2026, 4, 12));

        Assert.Equal(mora, exigible.Mora);
        Assert.Equal(base1 + base2 + mora, exigible.Total);
        Assert.All(exigible.Obligaciones, x => Assert.Equal(7, x.DiasMora));
    }

    [Fact]
    public void SinTasaMoratoria_UsaLaCompensatoria()
    {
        var cliente = Escenarios.ClienteJuego2();
        cliente.TasaMoratoria = 0m;

        Assert.Equal(motor.CalcularTedCompensatoria(cliente), motor.CalcularTedMoratoria(cliente));
    }

    [Fact]
    public void AplicarPrelacion_ImputaMoraLuegoInteresLuegoCapital()
    {
        Assert.Equal((10m, 0m, 0m), motor.AplicarPrelacion(10m, 15m, 20m, 100m));
        Assert.Equal((15m, 5m, 0m), motor.AplicarPrelacion(20m, 15m, 20m, 100m));
        Assert.Equal((15m, 20m, 65m), motor.AplicarPrelacion(100m, 15m, 20m, 100m));
    }

    [Fact]
    public void CalcularPago_ImputaGlobalmenteMoraInteresCapital()
    {
        var cliente = Escenarios.ClienteJuego1();
        var compra = Escenarios.AgregarCompra(cliente, motor, 1, "Refrigeradora", 900m, ModalidadCompra.Cuotas, 3, new DateTime(2026, 9, 15, 10, 30, 0));
        var cuota1 = compra.Cronogramas.Single(x => x.NroCuota == 1);
        var tedMora = Ted(Tem(TipoTasa.Efectiva, 0.80m));
        var mora = Interes(cuota1.CuotaFija, tedMora, 5);

        var pago = motor.CalcularPago(cliente, cuota1.CuotaFija + mora, new DateTime(2026, 10, 31));

        Assert.Equal(mora, pago.ImputacionMora);
        Assert.Equal(cuota1.Interes, pago.ImputacionInteres);
        Assert.Equal(cuota1.Amortizacion, pago.ImputacionCapital);
        Assert.Same(cuota1, Assert.Single(pago.Exigible.Obligaciones).Cuota);
    }

    [Fact]
    public void CalcularPago_RechazaPagoParcial()
    {
        var cliente = ClienteConFinDeMesVencido(out var total);

        var error = Assert.Throws<DomainException>(() => motor.CalcularPago(cliente, total - 0.01m, new DateTime(2026, 4, 5)));

        Assert.Contains("parciales", error.Message);
        Assert.Contains(total.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), error.Message);
    }

    [Fact]
    public void CalcularPago_RechazaPagoExcedente()
    {
        var cliente = ClienteConFinDeMesVencido(out var total);

        var error = Assert.Throws<DomainException>(() => motor.CalcularPago(cliente, total + 1m, new DateTime(2026, 4, 5)));

        Assert.Contains("excedan", error.Message);
        Assert.Contains(total.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), error.Message);
    }

    [Fact]
    public void CalcularPago_RechazaCuandoNoHayExigible()
    {
        var cliente = ClienteConFinDeMesVencido(out _);

        Assert.Throws<DomainException>(() => motor.CalcularPago(cliente, 10m, new DateTime(2026, 4, 1)));
    }

    [Fact]
    public void ValidarCompra_RechazaExcesoDeLimiteDeCredito()
    {
        var cliente = Escenarios.ClienteJuego2();
        Escenarios.AgregarCompra(cliente, motor, 1, "Arroz 50 kg", 450m, ModalidadCompra.FinDeMes, 1, new DateTime(2026, 3, 10));

        Assert.Throws<CreditLimitExceededException>(() => motor.ValidarCompra(cliente, 50.01m, ModalidadCompra.FinDeMes, 1));
        motor.ValidarCompra(cliente, 50m, ModalidadCompra.FinDeMes, 1);
    }

    [Fact]
    public void ValidarCompra_RechazaPlazoMayorAlMaximo()
    {
        var cliente = Escenarios.ClienteJuego1();

        var error = Assert.Throws<DomainException>(() => motor.ValidarCompra(cliente, 100m, ModalidadCompra.Cuotas, 7));

        Assert.Contains("plazo", error.Message);
        motor.ValidarCompra(cliente, 100m, ModalidadCompra.Cuotas, 6);
    }

    [Fact]
    public void ValidarProducto_RechazaModalidadNoPermitidaYOtraTienda()
    {
        var producto = new Producto { Id = 1, TiendaId = 10, Activo = true, PermiteFinDeMes = true, PermiteCuotas = false };

        Assert.Throws<DomainException>(() => motor.ValidarProducto(producto, 10, ModalidadCompra.Cuotas));
        Assert.Throws<DomainException>(() => motor.ValidarProducto(producto, 99, ModalidadCompra.FinDeMes));
        motor.ValidarProducto(producto, 10, ModalidadCompra.FinDeMes);
    }

    [Fact]
    public void CalcularProximoPago_SinVencidosDevuelveElMontoDeLaProximaFecha()
    {
        var cliente = ClienteConFinDeMesVencido(out var total);

        var proximo = motor.CalcularProximoPago(cliente, new DateTime(2026, 3, 30));

        Assert.Equal(new DateTime(2026, 4, 5), proximo.Fecha);
        Assert.Equal(total, proximo.Total);
    }

    private Cliente ClienteConFinDeMesVencido(out decimal total)
    {
        var cliente = Escenarios.ClienteJuego2();
        Escenarios.AgregarCompra(cliente, motor, 1, "Arroz 50 kg", 150m, ModalidadCompra.FinDeMes, 1, new DateTime(2026, 3, 10, 9, 0, 0));
        total = motor.CalcularExigible(cliente, new DateTime(2026, 4, 5)).Total;
        return cliente;
    }
}
