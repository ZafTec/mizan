using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Commands;
using Mizan.Application.Exceptions;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Mizan.Infrastructure.Services;
using Mizan.Tests.Infrastructure;
using Xunit;

namespace Mizan.Tests.Application;

/// <summary>
/// The client owns the grant flags. These tests pin that down, because the
/// previous shape let the trainer choose their own access to the client's data.
/// </summary>
public class TrainerGrantsTests
{
    private static readonly Guid ClientId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TrainerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid RelationshipId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static (MizanDbContext db, UpdateTrainerGrantsCommandHandler handler) Make(Guid actingAs)
    {
        var options = new DbContextOptionsBuilder<MizanDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new MizanDbContext(options);

        db.TrainerClientRelationships.Add(new TrainerClientRelationship
        {
            Id = RelationshipId,
            ClientId = ClientId,
            TrainerId = TrainerId,
            Status = "active",
            CanViewNutrition = true,
            CanViewWorkouts = true,
            CanViewMeasurements = true,
            CanMessage = true,
            CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();

        var currentUser = new FakeCurrentUser { UserId = actingAs };
        var auth = new TrainerAuthorizationService(db, currentUser);
        return (db, new UpdateTrainerGrantsCommandHandler(db, auth));
    }

    [Fact]
    public async Task Client_CanRevokeASingleAxis_WithoutTouchingTheOthers()
    {
        var (db, handler) = Make(actingAs: ClientId);

        await handler.Handle(
            new UpdateTrainerGrantsCommand(RelationshipId, CanViewMeasurements: false),
            CancellationToken.None);

        var r = await db.TrainerClientRelationships.SingleAsync();
        r.CanViewMeasurements.Should().BeFalse();
        r.CanViewNutrition.Should().BeTrue("an omitted field must not be silently widened or narrowed");
        r.CanViewWorkouts.Should().BeTrue();
        r.CanMessage.Should().BeTrue();
    }

    [Fact]
    public async Task Client_EndingRelationship_RevokesEveryAxis()
    {
        var (db, handler) = Make(actingAs: ClientId);

        await handler.Handle(
            new UpdateTrainerGrantsCommand(RelationshipId, End: true),
            CancellationToken.None);

        var r = await db.TrainerClientRelationships.SingleAsync();
        r.Status.Should().Be("ended");
        r.EndedAt.Should().NotBeNull();
        r.CanViewNutrition.Should().BeFalse();
        r.CanViewWorkouts.Should().BeFalse();
        r.CanViewMeasurements.Should().BeFalse();
        r.CanMessage.Should().BeFalse();
    }

    [Fact]
    public async Task Trainer_CannotChangeTheirOwnGrants()
    {
        var (_, handler) = Make(actingAs: TrainerId);

        var act = () => handler.Handle(
            new UpdateTrainerGrantsCommand(RelationshipId, CanViewMeasurements: true),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task Stranger_CannotChangeGrants()
    {
        var (_, handler) = Make(actingAs: Guid.NewGuid());

        var act = () => handler.Handle(
            new UpdateTrainerGrantsCommand(RelationshipId, CanViewNutrition: true),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public void TrainerRequest_DefaultsToSharingNothingButMessaging()
    {
        var command = new SendTrainerRequestCommand(ClientId, TrainerId);

        command.CanViewNutrition.Should().BeFalse();
        command.CanViewWorkouts.Should().BeFalse();
        command.CanViewMeasurements.Should().BeFalse();
        command.CanMessage.Should().BeTrue();
    }
}
