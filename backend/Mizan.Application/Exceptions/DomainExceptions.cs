namespace Mizan.Application.Exceptions;

public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
    public DomainException(string message, Exception innerException) : base(message, innerException) { }
}

public class EntityNotFoundException : DomainException
{
    public EntityNotFoundException(string entityName, object key)
        : base($"{entityName} with key '{key}' was not found.") { }

    public EntityNotFoundException(string message) : base(message) { }
}

public class ForbiddenAccessException : DomainException
{
    public ForbiddenAccessException(string message) : base(message) { }
    public ForbiddenAccessException() : base("You do not have permission to perform this action.") { }
}

public sealed class UpgradeRequiredException : ForbiddenAccessException
{
    public UpgradeRequiredException(string message) : base(message) { }
}

public class DomainValidationException : DomainException
{
    public DomainValidationException(string message) : base(message) { }
}

/// <summary>Bad email or password. Deliberately says nothing about which.</summary>
public sealed class InvalidCredentialsException : DomainException
{
    public InvalidCredentialsException() : base("Email or password is incorrect.") { }
}

/// <summary>
/// Correct credentials, unconfirmed address. Distinct from InvalidCredentials
/// so the sign-in screen can offer to resend rather than blaming the password.
/// </summary>
public sealed class EmailNotVerifiedException : DomainException
{
    public EmailNotVerifiedException() : base("Confirm your email address before signing in.") { }
}

public sealed class AccountLockedException : DomainException
{
    public AccountLockedException(DateTime until)
        : base($"Too many failed attempts. Try again after {until:HH:mm} UTC.") { }
}
