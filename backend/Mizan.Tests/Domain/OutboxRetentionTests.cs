using FluentAssertions;
using Mizan.Domain.Entities;
using Mizan.Domain.Outbox;
using Xunit;

namespace Mizan.Tests.OutboxRules;

/// <summary>Which finished outbox jobs the retention pass removes, and from how far back.</summary>
public class OutboxRetentionTests
{
    private static readonly DateTime Now = new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

    public static TheoryData<OutboxJobStatus, int, int, int?> Cases => new()
    {
        // status, keep succeeded days, keep dead-lettered days, cutoff in days before now (null: never removed)
        { OutboxJobStatus.Succeeded, 30, 90, 30 },
        { OutboxJobStatus.DeadLettered, 30, 90, 90 },
        { OutboxJobStatus.Succeeded, 7, 90, 7 },
        { OutboxJobStatus.DeadLettered, 30, 365, 365 },
        { OutboxJobStatus.Succeeded, 0, 90, 1 },
        { OutboxJobStatus.Pending, 30, 90, null },
        { OutboxJobStatus.Running, 30, 90, null },
        { OutboxJobStatus.Failed, 30, 90, null },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void AFinishedJobIsRemovedAfterItsStatusesWindow_WorkThatCanRunNever(
        OutboxJobStatus status, int keepSucceeded, int keepDeadLettered, int? daysBack)
    {
        OutboxRetention.CutoffFor(status, Now, keepSucceeded, keepDeadLettered)
            .Should().Be(daysBack is { } days ? Now.AddDays(-days) : null);
    }
}
