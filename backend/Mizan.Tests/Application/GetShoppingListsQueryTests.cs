using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Application.Queries;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Moq;
using Xunit;

namespace Mizan.Tests.Application;

public class GetShoppingListsQueryTests
{
    private readonly MizanDbContext _context;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;

    public GetShoppingListsQueryTests()
    {
        var options = new DbContextOptionsBuilder<MizanDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new MizanDbContext(options);
        _mockCurrentUserService = new Mock<ICurrentUserService>();
    }

    [Fact]
    public async Task Handle_ShouldReturnUserShoppingLists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _mockCurrentUserService.Setup(x => x.UserId).Returns(userId);

        var list1 = new ShoppingList { Id = Guid.NewGuid(), UserId = userId, Name = "List 1", UpdatedAt = DateTime.UtcNow };
        var list2 = new ShoppingList { Id = Guid.NewGuid(), UserId = userId, Name = "List 2", UpdatedAt = DateTime.UtcNow.AddMinutes(-5) };
        var otherList = new ShoppingList { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Name = "Other List" };

        _context.ShoppingLists.AddRange(list1, list2, otherList);
        await _context.SaveChangesAsync();

        var query = new GetShoppingListsQuery();
        var handler = new GetShoppingListsQueryHandler(_context, _mockCurrentUserService.Object);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(2);
        result.ShoppingLists.Should().HaveCount(2);
        result.ShoppingLists.Should().Contain(l => l.Name == "List 1");
        result.ShoppingLists.Should().Contain(l => l.Name == "List 2");
        result.ShoppingLists.Should().NotContain(l => l.Name == "Other List");
    }
}
