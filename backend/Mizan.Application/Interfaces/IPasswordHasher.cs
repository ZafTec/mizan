namespace Mizan.Application.Interfaces;

public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>True when the password matches. Constant-time inside.</summary>
    bool Verify(string hash, string password);

    /// <summary>A successful match may need replacing with the current hash format.</summary>
    bool Verify(string hash, string password, out bool needsRehash);
}
