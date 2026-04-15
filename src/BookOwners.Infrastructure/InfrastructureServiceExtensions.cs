using BookOwners.Application.Interfaces;
using BookOwners.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BookOwners.Infrastructure;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var bupaApiBaseUrl = configuration["BupaApi:BaseUrl"]
            ?? throw new InvalidOperationException("BupaApi:BaseUrl is not configured.");

        services.AddMemoryCache();

        services.AddHttpClient<IBookOwnerRepository, BookOwnerRepository>(client =>
        {
            client.BaseAddress = new Uri(bupaApiBaseUrl);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }
}