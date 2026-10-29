using System.Text;

namespace Mizan.Telegram.Bot;

/// <summary>
/// Message lines for Telegram. A message's lines end in <c>\n</c> on every platform;
/// <see cref="StringBuilder.AppendLine()"/> would end them in <see cref="Environment.NewLine"/>,
/// which is <c>\r\n</c> on Windows.
/// </summary>
internal static class TelegramLines
{
    public static StringBuilder Line(this StringBuilder text) => text.Append('\n');

    public static StringBuilder Line(this StringBuilder text, string? value) => text.Append(value).Append('\n');
}
