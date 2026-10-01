using Finvex.Domain;

namespace Finvex.Application;

public static class ValidacionesCliente
{
    public const int MaxMesesPermitido = 36;

    public static string? ValidarCondiciones(decimal limiteCredito, decimal tasaCompensatoria, decimal tasaMoratoria,
        int diaCorte, int diaPago, int maxMeses, TimeSpan horaCorte)
    {
        if (limiteCredito < 0) return "El límite de crédito no puede ser negativo.";
        if (tasaCompensatoria <= 0) return "La tasa compensatoria debe ser mayor que cero.";
        if (tasaMoratoria < 0) return "La tasa moratoria no puede ser negativa.";
        if (diaCorte is < 1 or > 28 || diaPago is < 1 or > 28) return "El día de corte y el día de pago deben estar entre 1 y 28.";
        if (maxMeses is < 1 or > MaxMesesPermitido) return $"El plazo máximo debe estar entre 1 y {MaxMesesPermitido} meses.";
        if (horaCorte < TimeSpan.Zero || horaCorte >= TimeSpan.FromDays(1)) return "La hora de corte debe estar entre 00:00:00 y 23:59:59.";
        return null;
    }
}

public sealed class ClienteService(IClienteRepository clientes, IFinancialEngineService financialEngine) : IClienteService
{
    public async Task<(Cliente? Cliente, string? Error)> ActualizarAsync(Cliente cliente, ActualizarClienteRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Dni) || request.Dni.Trim().Length != 8 || string.IsNullOrWhiteSpace(request.Nombres))
            return (null, "DNI y nombres son obligatorios; el DNI debe tener 8 caracteres.");
        if (request.Password is not null && string.IsNullOrWhiteSpace(request.Password))
            return (null, "La contraseña no puede estar vacía.");
        var maxMeses = request.MaxMeses ?? cliente.MaxMeses;
        var horaCorte = request.HoraCorte ?? cliente.HoraCorte;
        var error = ValidacionesCliente.ValidarCondiciones(request.LimiteCredito, request.TasaCompensatoria, request.TasaMoratoria,
            request.DiaCorte, request.DiaPago, maxMeses, horaCorte);
        if (error is not null) return (null, error);
        if (await clientes.ExisteDniAsync(cliente.TiendaId, request.Dni.Trim(), cliente.Id, cancellationToken))
            return (null, "El DNI ya se encuentra registrado en esta tienda.");

        cliente.Dni = request.Dni.Trim();
        cliente.NombresCompletos = request.Nombres.Trim();
        cliente.LimiteCredito = decimal.Round(request.LimiteCredito, 2, MidpointRounding.AwayFromZero);
        cliente.TipoTasa = request.TipoTasa;
        cliente.TasaCompensatoria = decimal.Round(request.TasaCompensatoria, 7, MidpointRounding.AwayFromZero);
        cliente.TasaMoratoria = decimal.Round(request.TasaMoratoria, 7, MidpointRounding.AwayFromZero);
        cliente.DiaCorte = request.DiaCorte;
        cliente.DiaPago = request.DiaPago;
        cliente.Moneda = request.Moneda ?? cliente.Moneda;
        cliente.MaxMeses = maxMeses;
        cliente.HoraCorte = horaCorte;
        if (request.Password is not null) cliente.PasswordHash = global::BCrypt.Net.BCrypt.HashPassword(request.Password);
        await clientes.GuardarAsync(cliente, cancellationToken);
        return (cliente, null);
    }

    public ClienteListItemResponse CrearItem(Cliente cliente) => new(
        cliente.Id, cliente.Dni, cliente.NombresCompletos, cliente.LimiteCredito, Estado(cliente), financialEngine.CalcularDeudaCapital(cliente));

    public ClienteDetalleResponse CrearDetalle(Cliente cliente)
    {
        var deuda = financialEngine.CalcularDeudaCapital(cliente);
        return new ClienteDetalleResponse(cliente.Id, cliente.Dni, cliente.NombresCompletos, cliente.Usuario, cliente.LimiteCredito,
            cliente.LimiteCredito - deuda, cliente.TipoTasa, cliente.TasaCompensatoria, cliente.TasaMoratoria, cliente.DiaCorte,
            cliente.DiaPago, cliente.Moneda, cliente.MaxMeses, cliente.HoraCorte, Estado(cliente), deuda);
    }

    private static string Estado(Cliente cliente) => cliente.Activo ? "Activo" : "Inactivo";
}
