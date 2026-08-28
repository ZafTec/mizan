namespace Mizan.Application.Interfaces;

public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>True when the password matches. Constant-time inside.</summary>
    bool Verify(string hash, string password);
}
