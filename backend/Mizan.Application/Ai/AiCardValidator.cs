using System.Text.Json;
using System.Text.Json.Serialization;
using Mizan.Contracts.Ai;

namespace Mizan.Application.Ai;

/// <summary>A card a tool proposes from its command's result, before it is checked.</summary>
public sealed record AiCardDraft(string Type, object Data);

/// <summary>
/// The gate every card passes before it leaves the API. The type must be one of
/// <see cref="AiCardTypes"/>, and the data must read back as that type's record with no member
/// missing, none unknown, and no null where the record says there is none. A card that fails is
/// dropped, never passed through in some looser shape: a surface renders a fixed component per
/// type and must be able to trust what it is given (docs/AI.md).
/// </summary>
public static class AiCardValidator
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
    };

    /// <summary>The card as it goes on the wire, or null with the reason it was refused.</summary>
    public static AiCardDto? Validate(string? type, JsonElement data, out string? reason)
    {
        if (type is null || !AiCardTypes.DataTypes.TryGetValue(type, out var dataType))
        {
            reason = $"'{type}' is not a card type.";
            return null;
        }
        if (data.ValueKind != JsonValueKind.Object)
        {
            reason = "A card's data must be an object.";
            return null;
        }

        try
        {
            var typed = data.Deserialize(dataType, Wire);
            if (typed is null)
            {
                reason = "The card has no data.";
                return null;
            }
            reason = null;
            return new AiCardDto(type, JsonSerializer.SerializeToElement(typed, dataType, Wire));
        }
        catch (JsonException ex)
        {
            reason = $"The data does not match the {type} card: {ex.Message}";
            return null;
        }
    }

    /// <summary>Checks a card a tool built. The data is serialized as the record it claims to be, then read back as the type's record.</summary>
    public static AiCardDto? Validate(AiCardDraft draft, out string? reason) =>
        Validate(draft.Type, JsonSerializer.SerializeToElement(draft.Data, draft.Data.GetType(), Wire), out reason);
}
