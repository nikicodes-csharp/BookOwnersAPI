using BookOwners.Application.Interfaces;
using BookOwners.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BookOwners.Application;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IBookService, BookService>();
        services.AddScoped<IBookGroupingService, BookGroupingService>();
        return services;
    }
}