namespace Mizan.Application.Interfaces;

public record UserAccessStatus(bool Exists, bool EmailVerified, bool IsBanned, string Role = "user")
{
    public bool IsAllowed => Exists && EmailVerified && !IsBanned;
}

public interface IUserStatusService
{
    Task<UserAccessStatus> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default);
}
