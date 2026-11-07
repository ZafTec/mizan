using System.Collections.Concurrent;
using System.Reflection;

namespace Mizan.Application.Behaviors;

/// <summary>
/// The last line of defence for the audit log. A command that carries a secret should say so
/// by implementing <see cref="Interfaces.IRedactedAudit"/> or <see cref="Interfaces.ISkipAudit"/>,
/// and a contract test holds every command to that. If one still slips through, the audit
/// behavior uses this to store "[redacted]" in place of any string property whose name looks
/// like a credential, so a miss never puts a token or password in the database.
/// </summary>
public static class AuditRedaction
{
    public const string Placeholder = "[redacted]";

    /// <summary>Case-insensitive fragments that mark a string property as a secret.</summary>
    public static readonly IReadOnlyList<string> SensitiveNameParts = ["Token", "Code", "Secret", "Password", "Key", "Otp", "Pin"];

    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> SensitiveByType = new();

    public static bool IsSensitiveName(string propertyName) =>
        SensitiveNameParts.Any(part => propertyName.Contains(part, StringComparison.OrdinalIgnoreCase));

    /// <summary>The public string properties of <paramref name="type"/> whose names look like a secret.</summary>
    public static IReadOnlyList<PropertyInfo> SensitiveProperties(Type type) =>
        SensitiveByType.GetOrAdd(type, static t => t
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(string) && p.GetIndexParameters().Length == 0 && IsSensitiveName(p.Name))
            .ToArray());

    /// <summary>
    /// The request as a property bag, with every sensitive string replaced by <see cref="Placeholder"/>.
    /// The keys are the property names, so the stored JSON keeps the shape a plain serialization would have.
    /// </summary>
    public static IReadOnlyDictionary<string, object?> Redact(object request)
    {
        var type = request.GetType();
        var sensitive = SensitiveProperties(type);
        var bag = new Dictionary<string, object?>();
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0 || !property.CanRead) continue;
            var value = property.GetValue(request);
            bag[property.Name] = sensitive.Contains(property) && value is not null ? Placeholder : value;
        }

        return bag;
    }
}
