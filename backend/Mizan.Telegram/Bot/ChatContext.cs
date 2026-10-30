using System.Net;
using System.Text.RegularExpressions;
using Mizan.Contracts.Ai;
using Mizan.Telegram.Backend;

namespace Mizan.Telegram.Bot;

/// <summary>
/// Tells the person's assistant conversation what a command or button just did
/// (<c>POST /api/Ai/conversation/context</c>, docs/AI.md#app-context), so a message typed next is read
/// against it. A note is a fact in plain words: what was pressed, what the API answered and, when the
/// chat is waiting on a button, the question. It is never an instruction, and the bot decides nothing
/// by it. Best effort: a person without the assistant still gets every command and button.
/// </summary>
public sealed partial class ChatContext
{
    /// <summary>How long a step that asks a question waits for typed words to count as its answer.</summary>
    public const int QuestionHoldMinutes = 15;

    /// <summary>The API's limit on one note.</summary>
    public const int MaxNoteLength = 1000;

    private readonly MizanApiClient _api;

    public ChatContext(MizanApiClient api) => _api = api;

    /// <summary>A finished step: nothing is waiting for the person's next message any more.</summary>
    public Task ReportAsync(ResolvedUser user, string note, CancellationToken ct) => SendAsync(user, note, 0, ct);

    /// <summary>A step whose buttons ask a question: the note waits for the person's next message.</summary>
    public Task AskedAsync(ResolvedUser user, string note, CancellationToken ct) => SendAsync(user, note, QuestionHoldMinutes, ct);

    /// <summary>"The /today command showed: …", the card as the person read it, without the markup.</summary>
    public static string Showed(string source, string cardHtml) => $"{source} showed the person: {Plain(cardHtml)}";

    /// <summary>A Telegram HTML card as plain words.</summary>
    internal static string Plain(string html) =>
        WebUtility.HtmlDecode(Tags().Replace(html, "")).Trim();

    private async Task SendAsync(ResolvedUser user, string note, int holdMinutes, CancellationToken ct)
    {
        var text = note.Length <= MaxNoteLength ? note : note[..(MaxNoteLength - 1)] + "…";
        try
        {
            await _api.SendForResultAsync<AiConversationDto>(
                user.UserId, HttpMethod.Post, "api/Ai/conversation/context", new AiContextNoteRequest(text, holdMinutes), ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            // Nothing in the chat depends on the assistant.
        }
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();
}
