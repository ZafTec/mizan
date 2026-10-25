using System.Text.Json;
using Mizan.Application.Interfaces;
using Mizan.Application.Planning;
using Mizan.Domain.Entities;

namespace Mizan.Infrastructure.Outbox;

/// <summary>
/// Runs one scheduled evening reminder. The rules live in <see cref="EveningReminders"/>; a
/// reminder that is no longer due (the plan ended, the window moved or closed, or one already
/// went out today) finishes without sending.
/// </summary>
public sealed class EveningReminderJobHandler : IOutboxHandler
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly EveningReminders _reminders;

    public EveningReminderJobHandler(EveningReminders reminders) => _reminders = reminders;

    public string Type => OutboxJobTypes.EveningReminder;

    public int Concurrency => 4;

    public async Task HandleAsync(string payload, CancellationToken cancellationToken)
    {
        EveningReminderJob job;
        try { job = JsonSerializer.Deserialize<EveningReminderJob>(payload, Json) ?? throw new OutboxPermanentException("The queued reminder was empty."); }
        catch (JsonException ex) { throw new OutboxPermanentException("The queued reminder could not be read.", ex); }

        await _reminders.SendAsync(job, cancellationToken);
    }
}
