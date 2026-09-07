using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Queries;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Application;

public class LogHistoryRangeTests
{
    [Fact]
    public async Task DateRangesFilterBeforePaging_AndRemainScopedToTheOwner()
    {
        using var db = new MizanDbContext(new DbContextOptionsBuilder<MizanDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var userId = Guid.NewGuid();
        var first = new DateOnly(2026, 8, 1);
        for (var day = 0; day < 5; day++)
        {
            db.Workouts.Add(new Workout { Id = Guid.NewGuid(), UserId = userId, WorkoutDate = first.AddDays(day), Name = $"Day {day}" });
            db.BodyMeasurements.Add(new BodyMeasurement { Id = Guid.NewGuid(), UserId = userId, MeasurementDate = first.AddDays(day), WeightKg = 70 + day });
        }
        db.Workouts.Add(new Workout { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), WorkoutDate = first.AddDays(2) });
        db.BodyMeasurements.Add(new BodyMeasurement { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), MeasurementDate = first.AddDays(2) });
        await db.SaveChangesAsync();

        var workouts = await new GetWorkoutsQueryHandler(db).Handle(new GetWorkoutsQuery
        {
            UserId = userId, From = first.AddDays(1), To = first.AddDays(3), PageSize = 2, Page = 2, SortBy = "date", SortOrder = "asc"
        }, default);
        var measurements = await new GetBodyMeasurementsQueryHandler(db).Handle(new GetBodyMeasurementsQuery
        {
            UserId = userId, From = first.AddDays(1), To = first.AddDays(3), PageSize = 2, Page = 2, SortBy = "date", SortOrder = "asc"
        }, default);

        workouts.TotalCount.Should().Be(3);
        workouts.Items.Should().ContainSingle().Which.WorkoutDate.Should().Be(first.AddDays(3));
        measurements.TotalCount.Should().Be(3);
        measurements.Items.Should().ContainSingle().Which.Date.Should().Be(first.AddDays(3).ToDateTime(TimeOnly.MinValue));
    }
}
