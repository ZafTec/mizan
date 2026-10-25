namespace Mizan.Domain.Features;

public sealed record FeatureFlagDefinition(string Key, string Description, bool DefaultEnabled);

/// <summary>
/// The flags the code knows. A key outside this list is refused, so a typo
/// cannot create a flag that nothing reads. Each one is checked at the API in
/// one place, and every surface (web, MCP, Telegram) reaches it there.
/// </summary>
public static class FeatureFlags
{
    public const string PlanningGenerate = "planning.generate";
    public const string Prices = "prices";
    public const string TelegramBot = "telegram.bot";
    public const string AiAssistant = "ai.assistant";
    public const string SocialFeed = "social.feed";

    public static readonly IReadOnlyList<FeatureFlagDefinition> All =
    [
        new(PlanningGenerate, "Drafting meal plans with the assistant.", true),
        new(Prices, "Food price lookups and price reports.", true),
        new(TelegramBot, "The Telegram bot's writes and the Mini App sign-in.", true),
        new(AiAssistant, "Assistant chat and food analysis from a photo or a sentence.", true),
        new(SocialFeed, "The social feed: reading it, posting, reactions and comments.", true),
    ];

    public static FeatureFlagDefinition? Find(string? key) =>
        All.FirstOrDefault(flag => string.Equals(flag.Key, key, StringComparison.Ordinal));

    public static bool IsKnown(string? key) => Find(key) is not null;

    /// <summary>
    /// What a household gets: its own override when it has one, then the stored
    /// default an administrator set, then the default the catalog ships with.
    /// </summary>
    public static bool Resolve(bool catalogDefault, bool? storedDefault, bool? householdOverride) =>
        householdOverride ?? storedDefault ?? catalogDefault;
}
