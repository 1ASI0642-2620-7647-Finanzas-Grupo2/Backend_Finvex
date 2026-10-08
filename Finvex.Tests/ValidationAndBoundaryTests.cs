using Finvex.Application;
using Finvex.Domain;

namespace Finvex.Tests;

public sealed class ValidationAndBoundaryTests
{
    [Theory]
    [InlineData(0, 5)]
    [InlineData(29, 5)]
    [InlineData(20, 0)]
    [InlineData(20, 29)]
    public void ValidarCondiciones_RechazaDiasFueraDeUnoAVeintiocho(int diaCorte, int diaPago)
    {
        var error = ValidacionesCliente.ValidarCondiciones(100m, 0.36m, 0m, diaCorte, diaPago, 12, new TimeSpan(23, 59, 59));

        Assert.Contains("entre 1 y 28", error);
    }

    [Fact]
    public void ValidarCondiciones_AceptaMoraCeroYRechazaCompensatoriaCero()
    {
        Assert.Null(ValidacionesCliente.ValidarCondiciones(0m, 0.36m, 0m, 20, 26, 1, TimeSpan.Zero));
        Assert.Contains("mayor que cero", ValidacionesCliente.ValidarCondiciones(100m, 0m, 0m, 20, 26, 1, TimeSpan.Zero));
    }

    [Fact]
    public void Corte_ExactamenteEnLaHoraPerteneceAlCicloYUnSegundoDespuesAlSiguiente()
    {
        var motor = new FinancialEngineService();
        var hora = new TimeSpan(18, 30, 0);

        Assert.Equal(new DateTime(2026, 2, 28, 18, 30, 0),
            motor.ObtenerFechaCorteCiclo(new DateTime(2026, 2, 28, 18, 30, 0), 28, hora));
        Assert.Equal(new DateTime(2026, 3, 28, 18, 30, 0),
            motor.ObtenerFechaCorteCiclo(new DateTime(2026, 2, 28, 18, 30, 1), 28, hora));
    }

    [Fact]
    public async Task Registro_RechazaDocumentosNoNumericosYPasswordCorto()
    {
        var servicio = new AuthService(new AuthRepositoryFalso());

        var tienda = await servicio.RegistrarTiendaAsync(
            new RegistrarAdminRequest("20A23456789", "Tienda", "Bodega", "admin", "123456"), default);
        var cliente = await servicio.RegistrarClienteAsync(1,
            new RegistrarClienteRequest("12345678", "Cliente", 100m, TipoTasa.Efectiva, 0.36m, 0m, 20, 26, "cliente", "123"), default);

        Assert.Null(tienda.Tienda);
        Assert.Contains("11 dígitos", tienda.Error);
        Assert.Null(cliente.Cliente);
        Assert.Contains("6 caracteres", cliente.Error);
    }

    [Fact]
    public async Task LoginCliente_ExigeElRucDeLaTienda()
    {
        var repositorio = new AuthRepositoryFalso
        {
            Cliente = new Cliente
            {
                Id = 8,
                TiendaId = 3,
                Usuario = "repetido",
                Activo = true,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secreto")
            }
        };
        var servicio = new AuthService(repositorio);

        Assert.Null(await servicio.AutenticarClienteAsync("repetido", "secreto", null, default));
        Assert.Null(await servicio.AutenticarClienteAsync("repetido", "secreto", "20111111111", default));
        var autenticado = await servicio.AutenticarClienteAsync("repetido", "secreto", AuthRepositoryFalso.Ruc, default);

        Assert.NotNull(autenticado);
        Assert.Equal(3, autenticado.TiendaId);
    }

    private sealed class AuthRepositoryFalso : IAuthRepository
    {
        public const string Ruc = "20123456789";
        public Cliente? Cliente { get; init; }

        public Task<Tienda?> ObtenerTiendaPorUsuarioAsync(string usuario, CancellationToken cancellationToken) => Task.FromResult<Tienda?>(null);
        public Task<Cliente?> ObtenerClientePorUsuarioAsync(string usuario, string tiendaRuc, CancellationToken cancellationToken) =>
            Task.FromResult(tiendaRuc == Ruc && Cliente?.Usuario == usuario ? Cliente : null);
        public Task<bool> ExisteTiendaAsync(string usuario, string ruc, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> ExisteClienteAsync(long tiendaId, string usuario, string dni, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task AgregarTiendaAsync(Tienda tienda, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task AgregarClienteAsync(Cliente cliente, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<AdministradorSistema?> ObtenerAdministradorSistemaPorUsuarioAsync(string usuario, CancellationToken cancellationToken) => Task.FromResult<AdministradorSistema?>(null);
        public Task AgregarAdministradorSistemaAsync(AdministradorSistema administrador, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
