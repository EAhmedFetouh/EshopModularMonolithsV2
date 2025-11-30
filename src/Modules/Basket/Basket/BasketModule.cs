using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Basket;

public static class BasketModule
{

    public static IServiceCollection AddBasketModule(this IServiceCollection services,
         IConfiguration configuration)
    {
        // Add services to the container.


        return services;
    }


    public static IApplicationBuilder UseBasketModule(this IApplicationBuilder app)
    {
        // Configure the HTTP request pipeline. 

        return app;
    }
}
