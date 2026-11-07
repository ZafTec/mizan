namespace Mizan.Application.OAuth;

/// <summary>An error the OAuth endpoints report in RFC 6749 form: an error code and a description.</summary>
public sealed class OAuthException : Exception
{
    public string Error { get; }
    public int Status { get; }

    public OAuthException(string error, string description, int status = 400) : base(description)
    {
        Error = error;
        Status = status;
    }
}
