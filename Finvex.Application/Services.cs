using System.Globalization;
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
        if (tem == 0) return decimal.Round(capital / plazoMeses, 2, MidpointRounding.AwayFromZero);
        var factor = (decimal)Math.Pow((double)(1m + tem), plazoMeses);
        return decimal.Round(capital * tem * factor / (factor - 1m), 2, MidpointRounding.AwayFromZero);
    }

    public IReadOnlyCollection<Cronograma> GenerarCronograma(Compra compra, Cliente cliente)
    {
        var tem = ConvertirATem(cliente.TipoTasa, cliente.TasaCompensatoria);
        var ted = CalcularTed(tem);
        var fechaPago = ObtenerFechaPagoCompra(compra, cliente);
        var diasGracia = CalcularDiasGracia(compra, cliente);
        var capitalAjustado = CalcularCapitalCapitalizado(compra.PrecioCredito, ted, diasGracia);
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
                FechaVencimiento = fechaPago.AddMonths(numero),
                SaldoInicial = decimal.Round(saldo, 2, MidpointRounding.AwayFromZero),
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

    public DateTime ObtenerFechaCorteCiclo(DateTime fecha, int diaCorte, TimeSpan horaCorte)
    {
        var corte = CrearFecha(fecha.Year, fecha.Month, diaCorte).Add(horaCorte);
        if (fecha <= corte) return corte;
        var siguiente = new DateTime(fecha.Year, fecha.Month, 1).AddMonths(1);
        return CrearFecha(siguiente.Year, siguiente.Month, diaCorte).Add(horaCorte);
    }

    public DateTime ObtenerFechaPagoCiclo(DateTime fechaCorte, int diaCorte, int diaPago)
    {
        var mes = new DateTime(fechaCorte.Year, fechaCorte.Month, 1);
        if (diaPago < diaCorte) mes = mes.AddMonths(1);
        return CrearFecha(mes.Year, mes.Month, diaPago);
    }

    public DateTime ObtenerFechaPagoCompra(Compra compra, Cliente cliente) => ObtenerFechaPagoCiclo(
        ObtenerFechaCorteCiclo(compra.FechaCompra, cliente.DiaCorte, cliente.HoraCorte), cliente.DiaCorte, cliente.DiaPago);

    public int CalcularDiasGracia(Compra compra, Cliente cliente) =>
        Math.Max(0, (ObtenerFechaPagoCompra(compra, cliente) - compra.FechaCompra.Date).Days);

    public decimal CalcularTedCompensatoria(Cliente cliente) =>
        CalcularTed(ConvertirATem(cliente.TipoTasa, cliente.TasaCompensatoria));

    public decimal CalcularTedMoratoria(Cliente cliente) =>
        CalcularTed(ConvertirATem(cliente.TipoTasa, cliente.TasaMoratoria > 0m ? cliente.TasaMoratoria : cliente.TasaCompensatoria));

    public decimal CalcularDeudaCapital(Cliente cliente) =>
        cliente.Compras.Where(x => x.Estado != EstadoCompra.Pagada).Sum(x => x.SaldoCapital);

    public void ValidarCompra(Cliente cliente, decimal monto, ModalidadCompra modalidad, int plazoMeses)
    {
        if (monto <= 0) throw new DomainException("El precio debe ser mayor que cero.");
        if (!Enum.IsDefined(modalidad)) throw new DomainException("La modalidad debe ser FinDeMes o Cuotas.");
        if (modalidad == ModalidadCompra.Cuotas && plazoMeses <= 0) throw new DomainException("El plazo es obligatorio para cuotas.");
        var plazo = modalidad == ModalidadCompra.Cuotas ? plazoMeses : 1;
        if (plazo > cliente.MaxMeses)
            throw new DomainException($"El plazo solicitado ({plazo} meses) excede el máximo permitido para el cliente ({cliente.MaxMeses} meses).");
        var disponible = cliente.LimiteCredito - CalcularDeudaCapital(cliente);
        if (monto > disponible) throw new CreditLimitExceededException(monto, disponible);
    }

    public IReadOnlyCollection<ObligacionPendiente> ObtenerObligaciones(Compra compra, Cliente cliente, DateTime fecha)
    {
        if (compra.Estado == EstadoCompra.Pagada) return Array.Empty<ObligacionPendiente>();
        var tedMora = CalcularTedMoratoria(cliente);
        if (compra.Modalidad == ModalidadCompra.Cuotas)
            return compra.Cronogramas
                .Where(x => x.Estado != EstadoCronograma.Pagada)
                .OrderBy(x => x.NroCuota)
                .Select(x => CrearObligacion(compra, x, x.FechaVencimiento, x.Amortizacion, x.Interes, DiasMesComercial, fecha, tedMora))
                .ToArray();

        var fechaPago = ObtenerFechaPagoCompra(compra, cliente);
        var dias = CalcularDiasGracia(compra, cliente);
        var interes = CalcularInteresDiario(compra.SaldoCapital, CalcularTedCompensatoria(cliente), dias);
        return new[] { CrearObligacion(compra, null, fechaPago, compra.SaldoCapital, interes, dias, fecha, tedMora) };
    }

    public ResumenExigible CalcularExigible(Cliente cliente, DateTime fecha)
    {
        var obligaciones = cliente.Compras
            .SelectMany(compra => ObtenerObligaciones(compra, cliente, fecha))
            .Where(x => x.FechaVencimiento.Date <= fecha.Date)
            .OrderBy(x => x.FechaVencimiento)
            .ThenBy(x => x.Compra.FechaCompra)
            .ThenBy(x => x.Cuota?.NroCuota ?? 0)
            .ToArray();
        return new ResumenExigible(fecha.Date, obligaciones,
            obligaciones.Sum(x => x.Mora), obligaciones.Sum(x => x.Interes), obligaciones.Sum(x => x.Capital), obligaciones.Sum(x => x.Total));
    }

    public ResumenExigible CalcularProximoPago(Cliente cliente, DateTime hoy)
    {
        var vencimientos = cliente.Compras
            .SelectMany(compra => ObtenerObligaciones(compra, cliente, hoy))
            .Select(x => x.FechaVencimiento.Date)
            .ToArray();
        if (vencimientos.Length == 0 || vencimientos.Any(x => x < hoy.Date)) return CalcularExigible(cliente, hoy);
        return CalcularExigible(cliente, vencimientos.Min());
    }

    public ResultadoPago CalcularPago(Cliente cliente, decimal monto, DateTime fechaPago)
    {
        var exigible = CalcularExigible(cliente, fechaPago);
        var fechaTexto = fechaPago.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        if (exigible.Total <= 0m) throw new DomainException($"El cliente no tiene montos exigibles al {fechaTexto}.");
        var montoPago = decimal.Round(monto, 2, MidpointRounding.AwayFromZero);
        var totalTexto = exigible.Total.ToString("0.00", CultureInfo.InvariantCulture);
        if (montoPago < exigible.Total)
            throw new DomainException($"No se aceptan pagos parciales. El monto exacto exigible al {fechaTexto} es {totalTexto}.");
        if (montoPago > exigible.Total)
            throw new DomainException($"No se aceptan pagos que excedan lo exigible. El monto exacto exigible al {fechaTexto} es {totalTexto}.");
        var (mora, interes, capital) = AplicarPrelacion(montoPago, exigible.Mora, exigible.Interes, exigible.Capital);
        return new ResultadoPago(exigible, montoPago, mora, interes, capital);
    }

    public void ValidarProducto(Producto producto, long tiendaId, ModalidadCompra modalidad)
    {
        if (producto.TiendaId != tiendaId) throw new DomainException("El producto no pertenece a la tienda.");
        if (!producto.Activo) throw new DomainException("El producto está inactivo.");
        if (modalidad == ModalidadCompra.FinDeMes && !producto.PermiteFinDeMes)
            throw new DomainException("El producto no permite la modalidad FinDeMes.");
        if (modalidad == ModalidadCompra.Cuotas && !producto.PermiteCuotas)
            throw new DomainException("El producto no permite la modalidad Cuotas.");
    }

    public DateTime ObtenerUltimoCorteCerrado(DateTime ahora, int diaCorte, TimeSpan horaCorte)
    {
        var corte = ObtenerFechaCorteCiclo(ahora, diaCorte, horaCorte);
        if (corte <= ahora) return corte;
        var anterior = new DateTime(corte.Year, corte.Month, 1).AddMonths(-1);
        return CrearFecha(anterior.Year, anterior.Month, diaCorte).Add(horaCorte);
    }

    public ListadoPagoResponse CalcularListadoPago(Cliente cliente, DateTime fechaCorte, DateTime fechaCalculo)
    {
        var corte = ObtenerFechaCorteCiclo(fechaCorte.Date, cliente.DiaCorte, cliente.HoraCorte);
        var mesAnterior = new DateTime(corte.Year, corte.Month, 1).AddMonths(-1);
        var corteAnterior = CrearFecha(mesAnterior.Year, mesAnterior.Month, cliente.DiaCorte).Add(cliente.HoraCorte);
        var fechaPago = ObtenerFechaPagoCiclo(corte, cliente.DiaCorte, cliente.DiaPago);
        var ted = CalcularTedCompensatoria(cliente);
        var items = new List<ItemListadoPagoResponse>();

        foreach (var compra in cliente.Compras.Where(x => x.FechaCompra > corteAnterior && x.FechaCompra <= corte).OrderBy(x => x.FechaCompra))
        {
            var dias = Math.Max(0, (fechaPago - compra.FechaCompra.Date).Days);
            if (compra.Modalidad == ModalidadCompra.FinDeMes)
            {
                var interes = CalcularInteresDiario(compra.PrecioCredito, ted, dias);
                items.Add(new ItemListadoPagoResponse(TipoItemListado.Compra, compra.Id, null, compra.Producto, compra.FechaCompra,
                    compra.PrecioCredito, dias, interes, compra.PrecioCredito + interes));
            }
            else
            {
                var interesGracia = CalcularCapitalCapitalizado(compra.PrecioCredito, ted, dias) - compra.PrecioCredito;
                items.Add(new ItemListadoPagoResponse(TipoItemListado.Compra, compra.Id, null,
                    $"{compra.Producto} (financiada en {compra.PlazoMeses} cuotas)", compra.FechaCompra, compra.PrecioCredito, dias, interesGracia, 0m));
            }
        }

        var cuotas = cliente.Compras
            .SelectMany(compra => compra.Cronogramas.Select(cuota => (Compra: compra, Cuota: cuota)))
            .Where(x => x.Cuota.FechaVencimiento.Date == fechaPago)
            .OrderBy(x => x.Compra.FechaCompra)
            .ThenBy(x => x.Cuota.NroCuota);
        foreach (var (compra, cuota) in cuotas)
            items.Add(new ItemListadoPagoResponse(TipoItemListado.Cuota, compra.Id, cuota.NroCuota,
                $"Cuota {cuota.NroCuota} de {compra.PlazoMeses} - {compra.Producto}", cuota.FechaVencimiento.Date,
                cuota.Amortizacion, DiasMesComercial, cuota.Interes, cuota.CuotaFija));

        var vencidas = cliente.Compras
            .SelectMany(compra => ObtenerObligaciones(compra, cliente, fechaCalculo))
            .Where(x => x.FechaVencimiento == fechaPago && x.DiasMora > 0)
            .ToArray();
        if (vencidas.Length > 0)
            items.Add(new ItemListadoPagoResponse(TipoItemListado.InteresMora, null, null, "Intereses por mora", fechaCalculo.Date,
                vencidas.Sum(x => x.Base), vencidas.Max(x => x.DiasMora), 0m, vencidas.Sum(x => x.Mora)));

        return new ListadoPagoResponse(cliente.Id, corte.Date, fechaPago, fechaCalculo, items.Sum(x => x.Monto), items);
    }

    private static decimal CalcularCapitalCapitalizado(decimal precio, decimal ted, int dias) =>
        decimal.Round(precio * (decimal)Math.Pow((double)(1m + ted), dias), 2, MidpointRounding.AwayFromZero);

    private ObligacionPendiente CrearObligacion(Compra compra, Cronograma? cuota, DateTime vencimiento, decimal capital,
        decimal interes, int diasInteres, DateTime fecha, decimal tedMora)
    {
        var diasMora = Math.Max(0, (fecha.Date - vencimiento.Date).Days);
        var mora = CalcularInteresDiario(capital + interes, tedMora, diasMora);
        return new ObligacionPendiente(compra, cuota, vencimiento.Date, capital, interes, diasInteres, mora, diasMora);
    }

    private static DateTime CrearFecha(int anio, int mes, int dia) =>
        new(anio, mes, Math.Min(dia, DateTime.DaysInMonth(anio, mes)));
}
