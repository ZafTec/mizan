using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Commands;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Application;

public class LogBodyMeasurementCommandTests
{
    private readonly MizanDbContext _context;

    public LogBodyMeasurementCommandTests()
    {
        var options = new DbContextOptionsBuilder<MizanDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new MizanDbContext(options);
    }

    [Fact]
    public async Task Handle_ShouldLogMeasurement()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var date = DateTime.UtcNow;
        var command = new LogBodyMeasurementCommand(
            userId,
            date,
            80.5m,
            20.0m,
            null, null, null, null, null, null, null, null,
            "Good progress"
        );
        var handler = new LogBodyMeasurementCommandHandler(_context);

        // Act
        var id = await handler.Handle(command, CancellationToken.None);

        // Assert
        var measurement = await _context.BodyMeasurements.FindAsync(id);
        measurement.Should().NotBeNull();
        measurement.UserId.Should().Be(userId);
        measurement.WeightKg.Should().Be(80.5m);
        measurement.Notes.Should().Be("Good progress");
    }
}
