using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mizan.Application.Ai.Tools;
using Mizan.Application.Exceptions;

namespace Mizan.Application.Tools;

/// <summary>
/// A task tool's arguments. Every failure names the argument, because the caller is often a
/// model that reads the message and tries again.
/// </summary>
public sealed class ToolArgs
{
    /// <summary>The JSON the API speaks: camelCase, enums as strings either way, numbers that may be quoted.</summary>
    public static readonly JsonSerializerOptions Json = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    public ToolArgs(JsonElement root) =>
        Root = root.ValueKind == JsonValueKind.Object ? root : JsonDocument.Parse("{}").RootElement.Clone();

    public static ToolArgs Empty { get; } = new(default);

    public static ToolArgs Parse(string? json) =>
        new(string.IsNullOrWhiteSpace(json) ? default : JsonDocument.Parse(json).RootElement.Clone());

    public JsonElement Root { get; }

    public bool Has(string field) =>
        Root.TryGetProperty(field, out var value) && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined);

    public string RequiredString(string field) => AiToolArgs.RequiredString(Root, field);
    public string? OptionalString(string field) => AiToolArgs.OptionalString(Root, field);
    public decimal RequiredDecimal(string field) => AiToolArgs.RequiredDecimal(Root, field);
    public decimal? OptionalDecimal(string field) => AiToolArgs.OptionalDecimal(Root, field);
    public int? OptionalInt(string field) => AiToolArgs.OptionalInt(Root, field);
    public Guid RequiredGuid(string field) => AiToolArgs.RequiredGuid(Root, field);
    public Guid? OptionalGuid(string field) => AiToolArgs.OptionalGuid(Root, field);
    public DateOnly? OptionalDate(string field) => AiToolArgs.OptionalDate(Root, field);
    public DateOnly RequiredDate(string field) =>
        OptionalDate(field) ?? throw new DomainValidationException($"'{field}' is required, as YYYY-MM-DD.");

    public bool? OptionalBool(string field)
    {
        if (!Root.TryGetProperty(field, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => throw new DomainValidationException($"'{field}' must be true or false."),
        };
    }

    public TEnum? OptionalEnum<TEnum>(string field) where TEnum : struct, Enum
    {
        var raw = OptionalString(field);
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return Enum.TryParse<TEnum>(raw.Replace("_", string.Empty), ignoreCase: true, out var parsed)
            ? parsed
            : throw new DomainValidationException($"'{field}' must be one of {string.Join(", ", Enum.GetNames<TEnum>())}.");
    }

    public List<string> StringList(string field)
    {
        if (!Has(field)) return [];
        var value = Root.GetProperty(field);
        if (value.ValueKind == JsonValueKind.String) return [value.GetString()!];
        if (value.ValueKind != JsonValueKind.Array) throw new DomainValidationException($"'{field}' must be a list.");
        return value.EnumerateArray().Select(v => v.ValueKind == JsonValueKind.String
            ? v.GetString()!
            : throw new DomainValidationException($"'{field}' must be a list of strings.")).ToList();
    }

    public List<Guid> GuidList(string field) =>
        StringList(field).Select(raw => Guid.TryParse(raw, out var id)
            ? id
            : throw new DomainValidationException($"'{field}' must hold ids returned by a tool.")).ToList();

    /// <summary>A nested object, such as one item of a list.</summary>
    public ToolArgs Object(string field) =>
        Has(field) && Root.GetProperty(field).ValueKind == JsonValueKind.Object
            ? new ToolArgs(Root.GetProperty(field))
            : throw new DomainValidationException($"'{field}' must be an object.");

    /// <summary>A nested object, or no arguments at all when it is left out.</summary>
    public ToolArgs ObjectOrEmpty(string field) => Has(field) ? Object(field) : Empty;

    /// <summary>The objects of a list argument, each as its own arguments.</summary>
    public List<ToolArgs> Objects(string field)
    {
        if (!Has(field)) return [];
        var value = Root.GetProperty(field);
        if (value.ValueKind != JsonValueKind.Array) throw new DomainValidationException($"'{field}' must be a list.");
        return value.EnumerateArray().Select((item, i) => item.ValueKind == JsonValueKind.Object
            ? new ToolArgs(item)
            : throw new DomainValidationException($"'{field}[{i}]' must be an object.")).ToList();
    }

    /// <summary>An argument read as a Contracts record, or null when it is left out.</summary>
    public T? As<T>(string field) where T : class
    {
        if (!Has(field)) return null;
        try
        {
            return Root.GetProperty(field).Deserialize<T>(Json);
        }
        catch (JsonException ex)
        {
            throw new DomainValidationException($"'{field}' does not have the expected shape: {ex.Message}");
        }
    }

    /// <summary>The arguments themselves read as a Contracts record, ignoring the mode argument and any other extra field.</summary>
    public T Body<T>() where T : class
    {
        try
        {
            return Root.Deserialize<T>(Json) ?? throw new DomainValidationException("The arguments are empty.");
        }
        catch (JsonException ex)
        {
            throw new DomainValidationException($"The arguments do not have the expected shape: {ex.Message}");
        }
    }

    /// <summary>A file sent as base64, or null when it is left out. A data URI is accepted as it is.</summary>
    public byte[]? OptionalBase64(string field)
    {
        var raw = OptionalString(field);
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var payload = raw.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ? raw[(raw.IndexOf(',') + 1)..] : raw;
        try
        {
            var bytes = Convert.FromBase64String(payload.Trim());
            return bytes.Length > 0 ? bytes : throw new DomainValidationException($"'{field}' decoded to nothing.");
        }
        catch (FormatException)
        {
            throw new DomainValidationException($"'{field}' is not valid base64.");
        }
    }

    public override string ToString() => Root.GetRawText();

    public static string Invariant(decimal value) => value.ToString(CultureInfo.InvariantCulture);
}
