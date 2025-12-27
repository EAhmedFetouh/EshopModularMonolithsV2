
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Shared.Messaing.Extensions;

public static class MassTransitExtensions
{
    public static IServiceCollection AddMassTransitWithAssemblies
        (this IServiceCollection services,IConfiguration configuration , params Assembly[] assemblies)
    {

        services.AddMassTransit(config=>
        {
            config.SetKebabCaseEndpointNameFormatter();

            config.SetInMemorySagaRepositoryProvider();

            config.AddConsumers(assemblies);
            config.AddSagaStateMachines(assemblies);
            config.AddSagas(assemblies);
            config.AddActivities(assemblies);

            config.UsingRabbitMq((context, configurator) =>
            {
                configurator.Host(new Uri(configuration["MessageBroker:Host"]!), h =>
                {
                    h.Username(configuration["MessageBroker:UserName"]!);
                    h.Password(configuration["MessageBroker:Password"]!);
                });
                configurator.ConfigureEndpoints(context);
            });

        });

        return services;
    }
}
