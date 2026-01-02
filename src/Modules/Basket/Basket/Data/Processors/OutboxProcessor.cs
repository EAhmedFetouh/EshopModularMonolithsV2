using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Basket.Data.Processors;

public class OutboxProcessor
    (IServiceProvider serviceProvider, IBus bus, ILogger<OutboxProcessor> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<BasketDbContext>();

                var outboxMessages = await dbContext.OutboxMessages
                                    .Where(m => m.ProcessedOn == null)
                                    .ToListAsync(stoppingToken);

                foreach (var message in outboxMessages)
                {
                    var eventType = Type.GetType(message.Type);
                    if (eventType == null)
                    {
                        logger.LogWarning("Could not resolve type: {type}", message.Type);
                        continue;
                    }

                    object? eventMessage;
                    try
                    {
                        eventMessage = JsonSerializer.Deserialize(message.Content, eventType, jsonOptions);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Could not deserialize message content for type: {type}. Content: {content}", message.Type, message.Content);
                        continue;
                    }

                    if (eventMessage == null)
                    {
                        logger.LogWarning("Deserialized message was null for type: {type}. Content: {content}", message.Type, message.Content);
                        continue;
                    }

                    await bus.Publish(eventMessage, stoppingToken);

                    message.ProcessedOn = DateTime.UtcNow;

                    logger.LogInformation("Successfully Processed outbox message with ID: {id}", message.Id);
                }

                await dbContext.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing outbox messages");
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}
