using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Commands;
using Mizan.Domain.Entities;
using Mizan.Infrastructure.Data;
using Xunit;

namespace Mizan.Tests.Application;

public class UpdateUserCommandTests
{
    private readonly MizanDbContext _context;

    public UpdateUserCommandTests()
    {
        var options = new DbContextOptionsBuilder<MizanDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new MizanDbContext(options);
    }

    [Fact]
    public async Task Handle_ShouldUpdateUser_WhenUserExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            Name = "Old Name"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var command = new UpdateUserCommand(userId, "New Name", "new-image.jpg");
        var handler = new UpdateUserCommandHandler(_context);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        var updatedUser = await _context.Users.FindAsync(userId);
        updatedUser.Name.Should().Be("New Name");
        updatedUser.Image.Should().Be("new-image.jpg");
    }

    [Fact]
    public async Task Handle_ShouldReturnFalse_WhenUserDoesNotExist()
    {
        // Arrange
        var command = new UpdateUserCommand(Guid.NewGuid(), "New Name", null);
        var handler = new UpdateUserCommandHandler(_context);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }
}
