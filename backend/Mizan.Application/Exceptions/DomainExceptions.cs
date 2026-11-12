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

/// <summary>
/// A connected app asked for a client's data that the client shares with their coach but has not
/// allowed AI to read. An outside agent needs both (docs/AI.md#consent).
/// </summary>
public sealed class ClientAiConsentRequiredException : ForbiddenAccessException
{
    public const string Code = "client_ai_consent_required";

    public ClientAiConsentRequiredException(string area)
        : base($"The client shares their {area} with you but has not allowed AI to read it, so a connected app cannot. They can change that in Mizan under Settings, AI.") { }
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
    public AiQuotaExceededException(
        Interfaces.AiQuotaScope scope, DateTime resetsAt, Interfaces.AiQuotaCap cap = Interfaces.AiQuotaCap.Allowance)
        : base(MessageFor(scope, resetsAt, cap))
    {
        Scope = scope;
        ResetsAt = resetsAt;
        Cap = cap;
    }

    public Interfaces.AiQuotaScope Scope { get; }
    public DateTime ResetsAt { get; }

    /// <summary>Which of the caller's caps tripped, so the message and the response can name it.</summary>
    public Interfaces.AiQuotaCap Cap { get; }

    private static string MessageFor(Interfaces.AiQuotaScope scope, DateTime resetsAt, Interfaces.AiQuotaCap cap)
    {
        if (scope == Interfaces.AiQuotaScope.Global)
            return $"The assistant is at capacity right now. Try again after {resetsAt:HH:mm} UTC.";

        return cap switch
        {
            Interfaces.AiQuotaCap.DailyRequests => $"You have reached your daily limit of AI requests. It resets at {resetsAt:HH:mm} UTC.",
            Interfaces.AiQuotaCap.DailyTokens => $"You have used your daily AI token allowance. It resets at {resetsAt:HH:mm} UTC.",
            Interfaces.AiQuotaCap.DailyCost => $"You have reached your daily AI spending cap. It resets at {resetsAt:HH:mm} UTC.",
            Interfaces.AiQuotaCap.MonthlyCost => $"You have reached your monthly AI spending cap. It resets on {resetsAt:yyyy-MM-dd} UTC.",
            _ => $"You have used your AI allowance for now. It resets at {resetsAt:HH:mm} UTC.",
        };
    }
}

/// <summary>
/// The feature is switched off for the caller's household, by its own override
/// or by the default (docs/ADMIN.md#feature-flags). Answered with 403
/// <c>feature_disabled</c>; every surface shows this message as it is.
/// </summary>
public sealed class FeatureDisabledException : ForbiddenAccessException
{
    public const string DefaultMessage = "This feature is turned off for your household.";

    public FeatureDisabledException(string featureKey) : base(DefaultMessage)
    {
        FeatureKey = featureKey;
    }

    public string FeatureKey { get; }
}

/// <summary>The provider is not configured, or answered with something unusable.</summary>
public class AiUnavailableException : DomainException
{
    public AiUnavailableException(string message, string reason = "unavailable") : base(message)
    {
        Reason = reason;
    }

    /// <summary>
    /// What went wrong, as a code the admin provider test reports: not_configured, timeout,
    /// unreachable, http_{status}, empty_response, limit_reached, rate_limited, blocked,
    /// account_blocked or unavailable. Never the provider's body.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// The stable code every surface answers with: the HTTP API's <c>errorCode</c>, a task tool's
    /// error, and what Telegram and MCP pass on (docs/AI.md#provider-errors).
    /// </summary>
    public virtual string ErrorCode => "ai_unavailable";

    /// <summary>"after 14:05 UTC" within the day, "on 2026-11-20 at 14:05 UTC" beyond it.</summary>
    protected static string After(DateTime at)
    {
        var utc = at.Kind == DateTimeKind.Local ? at.ToUniversalTime() : at;
        return utc - DateTime.UtcNow < TimeSpan.FromHours(20)
            ? $"after {utc:HH:mm} UTC"
            : $"on {utc:yyyy-MM-dd} at {utc:HH:mm} UTC";
    }
}

/// <summary>
/// The provider stopped the call because a usage limit or budget it enforces is spent (HTTP 402).
/// Not a fault in Mizan or the provider, so no administrator is told. Answered as 503
/// <c>ai_limit_reached</c>, with Retry-After when the provider said when it resets. Mizan's own
/// allowance is <see cref="AiQuotaExceededException"/>, a different thing.
/// </summary>
public sealed class AiLimitReachedException : AiUnavailableException
{
    public const string Code = "ai_limit_reached";

    public AiLimitReachedException(DateTime? resetsAt)
        : base(resetsAt is { } at
                ? $"The assistant has reached its limit for now. It should be back {After(at)}."
                : "The assistant has reached its limit for now.",
            "limit_reached")
    {
        ResetsAt = resetsAt;
    }

    /// <summary>When the provider said the limit resets, in UTC, if it said.</summary>
    public DateTime? ResetsAt { get; }

    public override string ErrorCode => Code;
}

/// <summary>
/// The provider asked us to slow down (HTTP 429). Answered as 429 <c>ai_rate_limited</c> with
/// Retry-After when the wait is known. The client retries once inside the call only when the wait
/// is a couple of seconds; a longer one is the person's to wait.
/// </summary>
public sealed class AiRateLimitedException : AiUnavailableException
{
    public const string Code = "ai_rate_limited";

    public AiRateLimitedException(TimeSpan? retryAfter)
        : base(MessageFor(retryAfter), "rate_limited")
    {
        RetryAfter = retryAfter is { } wait && wait < TimeSpan.Zero ? TimeSpan.Zero : retryAfter;
    }

    /// <summary>How long to wait before trying again, when the provider said.</summary>
    public TimeSpan? RetryAfter { get; }

    public override string ErrorCode => Code;

    private static string MessageFor(TimeSpan? retryAfter)
    {
        const string lead = "The assistant is busy right now.";
        if (retryAfter is not { } wait) return lead + " Try again shortly.";
        if (wait <= TimeSpan.FromSeconds(90)) return $"{lead} Try again in {Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds))} seconds.";
        if (wait <= TimeSpan.FromMinutes(90)) return $"{lead} Try again in {(int)Math.Ceiling(wait.TotalMinutes)} minutes.";
        if (wait <= TimeSpan.FromHours(36)) return $"{lead} Try again in {(int)Math.Ceiling(wait.TotalHours)} hours.";
        return $"{lead} Try again {After(DateTime.UtcNow + wait)}.";
    }
}

/// <summary>
/// The provider refused the content: a guardrail, a content filter or the model's own refusal.
/// Retrying the same words gives the same answer, so it is never retried and is not a provider
/// fault. Answered as 422 <c>ai_blocked</c>.
/// </summary>
public sealed class AiBlockedException : AiUnavailableException
{
    public const string Code = "ai_blocked";
    public const string DefaultMessage = "I can't help with that.";

    public AiBlockedException() : base(DefaultMessage, "blocked") { }

    public override string ErrorCode => Code;
}

/// <summary>
/// The provider will not serve this person: an administrator of the provider or gateway blocked
/// them. Answered as 403 <c>ai_account_blocked</c> with a neutral message.
/// </summary>
public sealed class AiAccountBlockedException : AiUnavailableException
{
    public const string Code = "ai_account_blocked";
    public const string DefaultMessage = "The assistant isn't available for your account.";

    public AiAccountBlockedException() : base(DefaultMessage, "account_blocked") { }

    public override string ErrorCode => Code;
}

/// <summary>
/// The provider answered a non-success status that is not a typed refusal (limit, rate, blocked;
/// docs/AI.md#provider-errors). The person sees <see cref="Exception.Message"/>: a configuration
/// error (400, 401, 403, 404, 422) says the assistant is misconfigured, because retrying never
/// fixes it; 5xx says to try again.
/// <see cref="ProviderMessage"/> is the provider's own explanation, cut short and with secrets
/// removed, for the log and the administrators, never for the person.
/// </summary>
public sealed class AiProviderRejectedException : AiUnavailableException
{
    public const string MisconfiguredMessage = "The assistant is misconfigured. An administrator has been told.";

    public AiProviderRejectedException(int status, string? providerType, string? providerCode, string? providerParam, string? providerMessage)
        : base(IsConfiguration(status)
                ? MisconfiguredMessage
                : $"The assistant returned an error ({status}). Try again shortly.",
            $"http_{status}")
    {
        Status = status;
        ProviderType = providerType;
        ProviderCode = providerCode;
        ProviderParam = providerParam;
        ProviderMessage = providerMessage;
    }

    public int Status { get; }
    public string? ProviderType { get; }
    public string? ProviderCode { get; }
    public string? ProviderParam { get; }
    public string? ProviderMessage { get; }

    /// <summary>A status that retrying does not change: the request or the configuration is wrong.</summary>
    public bool IsConfigurationError => IsConfiguration(Status);

    public static bool IsConfiguration(int status) => status is 400 or 401 or 403 or 404 or 422;
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

/// <summary>Too many of these in too short a time. Answered 429 with Retry-After.</summary>
public sealed class RateLimitedException : DomainException
{
    public RateLimitedException(string message, TimeSpan retryAfter) : base(message)
    {
        RetryAfter = retryAfter;
    }

    public TimeSpan RetryAfter { get; }
}

/// <summary>
/// A generated draft would have days without a food or recipe, so it is not saved. Answered as
/// 422 <c>plan_needs_foods</c> with the empty days and the missing food names.
/// </summary>
public sealed class PlanNeedsFoodsException : DomainException
{
    public const string Code = "plan_needs_foods";

    public PlanNeedsFoodsException(string message, IReadOnlyList<string> missingFoods, IReadOnlyList<int> emptyDays)
        : base(message)
    {
        MissingFoods = missingFoods;
        EmptyDays = emptyDays;
    }

    public IReadOnlyList<string> MissingFoods { get; }
    public IReadOnlyList<int> EmptyDays { get; }
}
