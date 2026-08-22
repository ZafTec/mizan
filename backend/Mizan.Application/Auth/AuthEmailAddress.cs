namespace Mizan.Application.Auth;

public static class AuthEmailAddress
{
    /// <summary>
    /// One canonical form so "Sam@Example.com" and "sam@example.com" are the
    /// same account. Applied on every read and write of the column.
    /// </summary>
    public static string Normalize(string email) => email.Trim().ToLowerInvariant();
}
