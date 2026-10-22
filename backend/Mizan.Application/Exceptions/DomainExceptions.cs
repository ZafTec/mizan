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

/// <summary>
/// A model call was refused because an allowance is spent. Carries which
/// ceiling tripped and when it resets, because "you are out of quota" and "the
/// service is at capacity" are different messages and conflating them is a
/// support ticket (docs/AI.md).
/// </summary>
public sealed class AiQuotaExceededException : DomainException
{
    public AiQuotaExceededException(Interfaces.AiQuotaScope scope, DateTime resetsAt)
        : base(scope == Interfaces.AiQuotaScope.User
            ? $"You have used your AI allowance for now. It resets at {resetsAt:HH:mm} UTC."
            : $"The assistant is at capacity right now. Try again after {resetsAt:HH:mm} UTC.")
    {
        Scope = scope;
        ResetsAt = resetsAt;
    }

    public Interfaces.AiQuotaScope Scope { get; }
    public DateTime ResetsAt { get; }
}

/// <summary>The provider is not configured, or answered with something unusable.</summary>
public sealed class AiUnavailableException : DomainException
{
    public AiUnavailableException(string message) : base(message) { }
}

/// <summary>The request is valid but the current state or configuration does not allow it. Answered with 409.</summary>
public sealed class ConflictException : DomainException
{
    public ConflictException(string message) : base(message) { }
}

/// <summary>A service the API depends on did not answer, or answered with an error. Answered with 502.</summary>
public sealed class UpstreamUnavailableException : DomainException
{
    public UpstreamUnavailableException(string message) : base(message) { }
}
