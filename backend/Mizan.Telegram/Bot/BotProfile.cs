using Mizan.Contracts.Telegram;

namespace Mizan.Telegram.Bot;

/// <summary>
/// What every chat with the bot shows before anyone types: the command list, and the button beside the input,
/// which opens the Mini App home when <c>TelegramBot:MiniAppUrl</c> (or <c>PublicUrl</c>) is HTTPS.
/// </summary>
public static class BotProfile
{
    public static async Task ApplyAsync(TelegramClient telegram, TelegramBotOptions options, CancellationToken ct)
    {
        await telegram.SetCommandsAsync(ct);
        await telegram.SetMenuButtonAsync(options.MiniAppButtonUrl(MiniAppPages.Home), ct);
    }
}
