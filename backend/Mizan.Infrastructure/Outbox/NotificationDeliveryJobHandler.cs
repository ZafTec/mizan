using System.Text.Json;
using Mizan.Application.Interfaces;
using Mizan.Application.Notifications;
using Mizan.Domain.Entities;

namespace Mizan.Infrastructure.Outbox;

/// <summary>
/// Sends one occurrence of a timed schedule to one person on one channel. The rules live in
/// <see cref="NotificationDeliveries"/>: a schedule turned off or deleted since the scan sends nothing.
/// </summary>
public sealed class NotificationDeliveryJobHandler : IOutboxHandler
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly NotificationDeliveries _deliveries;

    public NotificationDeliveryJobHandler(NotificationDeliveries deliveries) => _deliveries = deliveries;

    public string Type => OutboxJobTypes.NotificationDelivery;

    public int Concurrency => 4;

    public async Task HandleAsync(string payload, CancellationToken cancellationToken)
    {
        NotificationDeliveryJob job;
        try { job = JsonSerializer.Deserialize<NotificationDeliveryJob>(payload, Json) ?? throw new OutboxPermanentException("The queued notification was empty."); }
        catch (JsonException ex) { throw new OutboxPermanentException("The queued notification could not be read.", ex); }

        await _deliveries.SendAsync(job, cancellationToken);
    }
}
