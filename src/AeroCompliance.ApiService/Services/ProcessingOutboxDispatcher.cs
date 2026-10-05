using System.Text;
using AeroCompliance.Contracts.Messaging;
using AeroCompliance.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;

namespace AeroCompliance.ApiService.Services;

public sealed class ProcessingOutboxDispatcher(IServiceScopeFactory scopes, IConnection connection, ILogger<ProcessingOutboxDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await DispatchAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogWarning(ex, "Outbox dispatch cycle failed; messages remain pending."); }
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }

    private async Task DispatchAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AeroComplianceDbContext>();
        var pending = await db.OutboxMessages.Where(x => x.PublishedAt == null).OrderBy(x => x.CreatedAt).Take(20).ToListAsync(ct);
        if (pending.Count == 0) return;
        var options = new CreateChannelOptions(true, true);
        await using var channel = await connection.CreateChannelAsync(options, ct);
        await channel.ExchangeDeclareAsync(MessagingConstants.ProcessingExchange, ExchangeType.Topic, true, false, cancellationToken: ct);
        await channel.QueueDeclareAsync(MessagingConstants.AiProcessingQueue, true, false, false,
            new Dictionary<string, object?> { ["x-dead-letter-exchange"] = MessagingConstants.ProcessingExchange, ["x-dead-letter-routing-key"] = "image.processing.dead.v1" }, cancellationToken: ct);
        await channel.QueueDeclareAsync(MessagingConstants.AiProcessingDeadLetterQueue, true, false, false, cancellationToken: ct);
        await channel.QueueBindAsync(MessagingConstants.AiProcessingQueue, MessagingConstants.ProcessingExchange, MessagingConstants.ProcessingRequestRoutingKey, cancellationToken: ct);
        await channel.QueueBindAsync(MessagingConstants.AiProcessingDeadLetterQueue, MessagingConstants.ProcessingExchange, "image.processing.dead.v1", cancellationToken: ct);
        foreach (var message in pending)
        {
            try
            {
                var props = new BasicProperties { ContentType = "application/json", DeliveryMode = DeliveryModes.Persistent, MessageId = message.EventId.ToString(), Type = message.Type };
                await channel.BasicPublishAsync(MessagingConstants.ProcessingExchange, message.RoutingKey, false, props, Encoding.UTF8.GetBytes(message.Payload), ct);
                message.PublishedAt = DateTimeOffset.UtcNow; message.AttemptCount++; message.LastError = null;
            }
            catch (Exception ex) { message.AttemptCount++; message.LastError = Safe(ex); }
        }
        await db.SaveChangesAsync(ct);
    }

    private static string Safe(Exception ex) => $"{ex.GetType().Name}: publish failed";
}
