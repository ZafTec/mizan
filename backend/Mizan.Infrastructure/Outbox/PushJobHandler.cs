using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Infrastructure.Outbox;

public sealed record PushJob(Guid UserId, Guid NotificationId, string Title, string? Body, string? LinkUrl);

/// <summary>
/// Sends one notification to every phone the user has registered. A device the provider no
/// longer knows is forgotten. If every send failed the job is retried; if some got through
/// it is not, because a retry would notify the phones that already heard.
/// </summary>
public sealed class PushJobHandler : IOutboxHandler
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IMizanDbContext _context;
    private readonly IPushSender _sender;

    public PushJobHandler(IMizanDbContext context, IPushSender sender)
    {
        _context = context;
        _sender = sender;
    }

    public string Type => OutboxJobTypes.Push;

    public int Concurrency => 4;

    public async Task HandleAsync(string payload, CancellationToken cancellationToken)
    {
        PushJob job;
        try { job = JsonSerializer.Deserialize<PushJob>(payload, Json) ?? throw new OutboxPermanentException("The queued push was empty."); }
        catch (JsonException ex) { throw new OutboxPermanentException("The queued push could not be read.", ex); }

        if (!_sender.IsConfigured) return;

        var devices = await _context.DeviceTokens.Where(d => d.UserId == job.UserId).ToListAsync(cancellationToken);
        if (devices.Count == 0) return;

        var sent = 0;
        var failed = 0;
        var gone = new List<Guid>();
        foreach (var device in devices)
        {
            var outcome = await _sender.SendAsync(
                new PushMessage(device.Token, job.Title, job.Body, job.LinkUrl, job.NotificationId.ToString()), cancellationToken);
            switch (outcome)
            {
                case PushOutcome.Sent: sent++; break;
                case PushOutcome.TokenInvalid: gone.Add(device.Id); break;
                default: failed++; break;
            }
        }

        if (gone.Count > 0)
            await _context.DeviceTokens.Where(d => gone.Contains(d.Id)).ExecuteDeleteAsync(cancellationToken);

        if (sent == 0 && failed > 0)
            throw new InvalidOperationException("The push provider did not accept the notification.");
    }
}
