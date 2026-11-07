namespace Mizan.Application.Interfaces;

/// <summary>
/// For a command that carries a secret (a password, a one-time code, a token)
/// but is still worth auditing. The audit log stores <see cref="AuditDetails"/>
/// instead of serializing the whole request, so the event is recorded and the
/// secret is not.
/// </summary>
public interface IRedactedAudit
{
    object AuditDetails { get; }
}
