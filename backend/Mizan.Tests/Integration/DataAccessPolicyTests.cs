using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Mizan.Application.Interfaces;
using Mizan.Domain.Ai;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Integration;

/// <summary>
/// The policy decides who may read whose body weight, so it gets tested like
/// it matters. The cases that would leak are the interesting ones.
/// </summary>
[Collection("ApiIntegration")]
public class DataAccessPolicyTests
{
    private readonly ApiTestFixture _fixture;

    public DataAccessPolicyTests(ApiTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task YourOwnDataIsYoursToRead()
    {
        var me = await UserAsync();
        var axes = await AxesAsync(me, me, AccessPurpose.Display);

        axes.Should().BeEquivalentTo([DataAxis.Nutrition, DataAxis.Training, DataAxis.Body]);
    }

    /// <summary>
    /// Reading your own log in the app and sending it to a model are separate
    /// decisions, and the second defaults to no.
    /// </summary>
    [Fact]
    public async Task TheAiSeesNothingUntilYouSaySo()
    {
        var me = await UserAsync();

        (await AxesAsync(me, me, AccessPurpose.AiContext)).Should().BeEmpty();
    }

    [Fact]
    public async Task ConsentIsPerAxis()
    {
        var me = await UserAsync();
        await ConsentAsync(me, enabled: true, nutrition: true, training: false, body: false);

        (await AxesAsync(me, me, AccessPurpose.AiContext)).Should().BeEquivalentTo([DataAxis.Nutrition]);
    }

    [Fact]
    public async Task TheMasterSwitchBeatsEveryAxis()
    {
        var me = await UserAsync();
        await ConsentAsync(me, enabled: false, nutrition: true, training: true, body: true);

        (await AxesAsync(me, me, AccessPurpose.AiContext)).Should().BeEmpty();
    }

    [Fact]
    public async Task ATrainerSeesOnlyWhatTheClientGranted()
    {
        var (trainer, client) = await PairAsync(nutrition: true, workouts: false, measurements: false);

        (await AxesAsync(trainer, client, AccessPurpose.Display))
            .Should().BeEquivalentTo([DataAxis.Nutrition]);
    }

    /// <summary>
    /// The axis that was declared, defaulted false, settable by the client -
    /// and read by nothing. It is enforced now, before an endpoint exists that
    /// could have leaked it (docs/REFOCUS.md §11).
    /// </summary>
    [Fact]
    public async Task MeasurementsAreGatedLikeTheOtherTwo()
    {
        var (trainer, client) = await PairAsync(nutrition: false, workouts: false, measurements: true);

        var axes = await AxesAsync(trainer, client, AccessPurpose.Display);

        axes.Should().BeEquivalentTo([DataAxis.Body]);
        (await CanReadAsync(trainer, client, DataAxis.Nutrition, AccessPurpose.Display)).Should().BeFalse();
    }

    /// <summary>
    /// The intersection rule. A client who shares workouts with their coach but
    /// wants no AI involvement gets exactly that.
    /// </summary>
    [Fact]
    public async Task ATrainerGrantIsNotAiConsent()
    {
        var (trainer, client) = await PairAsync(nutrition: true, workouts: true, measurements: true);

        (await AxesAsync(trainer, client, AccessPurpose.Display)).Should().HaveCount(3);
        (await AxesAsync(trainer, client, AccessPurpose.AiContext)).Should().BeEmpty();
    }

    [Fact]
    public async Task TheAiSeesTheIntersectionOfGrantAndConsent()
    {
        var (trainer, client) = await PairAsync(nutrition: true, workouts: true, measurements: false);
        await ConsentAsync(client, enabled: true, nutrition: true, training: false, body: true);

        // Granted: nutrition, training. Consented: nutrition, body.
        (await AxesAsync(trainer, client, AccessPurpose.AiContext))
            .Should().BeEquivalentTo([DataAxis.Nutrition]);
    }

    [Fact]
    public async Task AStrangerSeesNothing()
    {
        var me = await UserAsync();
        var stranger = await UserAsync();

        (await AxesAsync(stranger, me, AccessPurpose.Display)).Should().BeEmpty();
        (await AxesAsync(stranger, me, AccessPurpose.AiContext)).Should().BeEmpty();
    }

    [Fact]
    public async Task AnEndedRelationshipGrantsNothing()
    {
        var (trainer, client) = await PairAsync(nutrition: true, workouts: true, measurements: true, status: "ended");

        (await AxesAsync(trainer, client, AccessPurpose.Display)).Should().BeEmpty();
    }

    private async Task<Guid> UserAsync()
    {
        var id = Guid.NewGuid();
        await _fixture.SeedUserAsync(id, $"policy-{id:N}@example.com", emailVerified: true);
        return id;
    }

    private async Task<(Guid Trainer, Guid Client)> PairAsync(
        bool nutrition, bool workouts, bool measurements, string status = "active")
    {
        var trainer = await UserAsync();
        var client = await UserAsync();

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        db.TrainerClientRelationships.Add(new TrainerClientRelationship
        {
            Id = Guid.CreateVersion7(),
            TrainerId = trainer,
            ClientId = client,
            Status = status,
            CanViewNutrition = nutrition,
            CanViewWorkouts = workouts,
            CanViewMeasurements = measurements,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        return (trainer, client);
    }

    private async Task ConsentAsync(Guid userId, bool enabled, bool nutrition, bool training, bool body)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
        db.UserAiConsents.Add(new UserAiConsent
        {
            UserId = userId,
            Enabled = enabled,
            ShareNutrition = nutrition,
            ShareTraining = training,
            ShareBody = body,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private async Task<IReadOnlySet<DataAxis>> AxesAsync(Guid principal, Guid subject, AccessPurpose purpose)
    {
        using var scope = _fixture.Services.CreateScope();
        var policy = scope.ServiceProvider.GetRequiredService<IDataAccessPolicy>();
        return await policy.ReadableAxesAsync(principal, subject, purpose);
    }

    private async Task<bool> CanReadAsync(Guid principal, Guid subject, DataAxis axis, AccessPurpose purpose)
    {
        using var scope = _fixture.Services.CreateScope();
        var policy = scope.ServiceProvider.GetRequiredService<IDataAccessPolicy>();
        return await policy.CanReadAsync(principal, subject, axis, purpose);
    }
}
