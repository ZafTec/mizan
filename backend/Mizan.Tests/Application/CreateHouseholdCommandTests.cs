using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Commands;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Application;

public class CreateHouseholdCommandTests
{
    private readonly MizanDbContext _context;

    public CreateHouseholdCommandTests()
    {
        var options = new DbContextOptionsBuilder<MizanDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new MizanDbContext(options);
    }

    [Fact]
    public async Task Handle_ShouldCreateHouseholdAndAdminMember()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateHouseholdCommand("My Family", userId);
        var handler = new CreateHouseholdCommandHandler(_context);

        // Act
        var householdId = await handler.Handle(command, CancellationToken.None);

        // Assert
        var household = await _context.Households.FindAsync(householdId);
        household.Should().NotBeNull();
        household.Name.Should().Be("My Family");
        household.CreatedBy.Should().Be(userId);

        var member = await _context.HouseholdMembers.FirstOrDefaultAsync(m => m.HouseholdId == householdId && m.UserId == userId);
        member.Should().NotBeNull();
        member.Role.Should().Be("admin");
        member.CanEditRecipes.Should().BeTrue();
    }
}
