using Basket.Data.Repository;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Behaviors;
using Shared.Data;
using Shared.Data.Interceptors;

namespace Basket;

public static class BasketModule
{

    public static IServiceCollection AddBasketModule(this IServiceCollection services,
         IConfiguration configuration)
    {
        // Add services to the container.
        //1. Api Endpoint services

        //2. Application Use Case services
         services.AddScoped<IBasketRepository, BasketRepository>();
         services.Decorate<IBasketRepository, CachedBasketRepository>();

        // this is manual decoration alternative

        //services.AddScoped<IBasketRepository>(provider =>
        //{
        //   var basketRepository = provider.GetRequiredService<BasketRepository>();
        //    return new CachedBasketRepository(
        //        basketRepository,
        //        provider.GetRequiredService<IDistributedCache>());
        //});


        //3. Data - Infrastructure services


        var connectionString = configuration.GetConnectionString("Database");

        services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();

        services.AddDbContext<BasketDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseNpgsql(connectionString);
        });


        return services;
    }


    public static IApplicationBuilder UseBasketModule(this IApplicationBuilder app)
    {
        // Configure the HTTP request pipeline. 

        //1. Api Endpoint services

        //2. Application Use Case services


        //3. Data - Infrastructure services
        app.UseMigration<BasketDbContext>();

        return app;
    }
}
