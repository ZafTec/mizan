using System.Text.Json;
using FluentAssertions;
using Mizan.Application.Ai;
using Mizan.Contracts.Ai;
using Xunit;

namespace Mizan.Tests.Application;

/// <summary>
/// A card reaches a surface only as one of the four known types with data that is exactly that
/// type's record. Everything else is refused, so no surface ever renders a shape it was not built for.
/// </summary>
public class AiCardValidatorTests
{
    private const string ListId = "8a7c6b2e-1f3d-4c5b-9a8e-7d6c5b4a3f21";

    public static TheoryData<string, string, string, bool> Cases => new()
    {
        { "a shopping list card", AiCardTypes.ShoppingList, $$"""{"listId":"{{ListId}}","itemCount":12,"needsReviewCount":1}""", true },
        { "an unknown type", "recipe_html", $$"""{"listId":"{{ListId}}","itemCount":12,"needsReviewCount":1}""", false },
        { "a known type in other letters", "Shopping_List", $$"""{"listId":"{{ListId}}","itemCount":12,"needsReviewCount":1}""", false },
        { "a member the record does not have", AiCardTypes.ShoppingList, $$"""{"listId":"{{ListId}}","itemCount":12,"needsReviewCount":1,"html":"<b>hi</b>"}""", false },
        { "a missing member", AiCardTypes.ShoppingList, $$"""{"listId":"{{ListId}}","itemCount":12}""", false },
        { "a null where the record has none", AiCardTypes.PlanPreview, $$"""{"planId":"{{ListId}}","name":null,"status":null,"startDate":null,"durationDays":7,"entryCount":0,"unresolvedCount":0,"budgetCeiling":null,"weeklyCost":null,"currency":null,"violations":[]}""", false },
        { "a plan preview with its optional fields empty", AiCardTypes.PlanPreview, $$"""{"planId":"{{ListId}}","name":null,"status":"Draft","startDate":null,"durationDays":7,"entryCount":0,"unresolvedCount":0,"budgetCeiling":null,"weeklyCost":null,"currency":null,"violations":[]}""", true },
        { "data that is not an object", AiCardTypes.CostTable, "\"a table\"", false },
        { "a number where the record has an id", AiCardTypes.ShoppingList, """{"listId":42,"itemCount":12,"needsReviewCount":1}""", false },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void ACardPassesOnlyAsAKnownTypeWithExactlyItsRecord(string _, string type, string data, bool accepted)
    {
        using var document = JsonDocument.Parse(data);

        var card = AiCardValidator.Validate(type, document.RootElement, out var reason);

        if (accepted)
        {
            card.Should().NotBeNull(reason);
            card!.Type.Should().Be(type);
        }
        else
        {
            card.Should().BeNull();
            reason.Should().NotBeNullOrWhiteSpace();
        }
    }
}
