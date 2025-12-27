using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Commands;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Application;

public class CreateShoppingListCommandTests
{
    private readonly MizanDbContext _context;

    public CreateShoppingListCommandTests()
    {
        var options = new DbContextOptionsBuilder<MizanDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new MizanDbContext(options);
    }

    [Fact]
    public async Task Handle_ShouldCreateShoppingList()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateShoppingListCommand("Groceries", userId, null);
        var handler = new CreateShoppingListCommandHandler(_context);

        // Act
        var listId = await handler.Handle(command, CancellationToken.None);

        // Assert
        var list = await _context.ShoppingLists.FindAsync(listId);
        list.Should().NotBeNull();
        list.Name.Should().Be("Groceries");
        list.UserId.Should().Be(userId);
        list.HouseholdId.Should().BeNull();
    }
}
