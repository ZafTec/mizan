namespace Mizan.Application.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    string? Role { get; }
    string? IpAddress { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(string role);
}
