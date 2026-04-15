namespace ErpBoulder.Infrastructure;

using ErpBoulder.Application.Configuration;
using ErpBoulder.Application.Interfaces.Security;
using ErpBoulder.Application.Interfaces.Services;
using ErpBoulder.Infrastructure.Data;
using ErpBoulder.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection(JwtSettings.SectionName);
        var jwtSettings = new JwtSettings
        {
            Issuer = jwtSection["Issuer"] ?? "ErpBoulder",
            Audience = jwtSection["Audience"] ?? "ErpBoulder.Frontend",
            Key = jwtSection["Key"] ?? "super-secret-development-key-change-me",
            AccessTokenMinutes = int.TryParse(jwtSection["AccessTokenMinutes"], out var accessMinutes) ? accessMinutes : 480,
            RefreshTokenDays = int.TryParse(jwtSection["RefreshTokenDays"], out var refreshDays) ? refreshDays : 30
        };
        services.AddSingleton(Options.Create(jwtSettings));

        services.AddDbContext<ErpBoulderDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAdministracionService, AdministracionService>();
        services.AddScoped<IVentasService, VentasService>();
        services.AddScoped<IOperacionService, OperacionService>();
        services.AddScoped<IReportesService, ReportesService>();

        return services;
    }
}
