namespace Mizan.Domain.Entities;

/// <summary>
/// A record a phone keeps a copy of. The database context stamps <see cref="UpdatedAt"/> on every
/// save, and writes a <see cref="DeletedRecord"/> when one is deleted, which is what lets an app
/// ask "what changed since I last looked" (docs/ARCHITECTURE.md#native-clients).
/// </summary>
public interface IChangeTracked
{
    Guid Id { get; }
    Guid UserId { get; }
    DateTime UpdatedAt { get; set; }
}
