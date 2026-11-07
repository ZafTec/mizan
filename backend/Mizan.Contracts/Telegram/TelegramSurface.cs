namespace Mizan.Contracts.Telegram;

/// <summary>
/// The bot names itself on every call it makes as a linked user, so the API can
/// hold the bot's writes to the <c>telegram.bot</c> feature flag. The header can
/// only add a check, never remove one, so the API need not trust who sent it.
/// </summary>
public static class TelegramSurface
{
    public const string Header = "X-Mizan-Surface";
    public const string Value = "telegram";
}
