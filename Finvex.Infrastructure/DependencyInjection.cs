using Finvex.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Finvex.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Finvex");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:Finvex debe configurarse mediante User Secrets o variables de entorno.");
        services.AddDbContext<FinvexDbContext>(options => options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
        services.AddScoped<IClienteRepository, ClienteRepository>();
        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ITiendaRepository, TiendaRepository>();
        services.AddScoped<IProductoRepository, ProductoRepository>();
        services.AddScoped<IListadoPagoRepository, ListadoPagoRepository>();
        services.AddScoped<IAuditoriaRepository, AuditoriaRepository>();
        services.AddScoped<AuditoriaService>();
        services.AddScoped<IAuditoriaService>(provider => provider.GetRequiredService<AuditoriaService>());
        return services;
    }
}
