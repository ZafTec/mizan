namespace Mizan.Infrastructure.Outbox;

public class OutboxOptions
{
    public const string SectionName = "Outbox";

    /// <summary>
    /// How often the dispatcher looks for work. Short enough that a
    /// verification email feels immediate, long enough that an idle system is
    /// not doing a query a second.
    /// </summary>
    public int PollSeconds { get; set; } = 3;

    /// <summary>
    /// After this many failures a job is dead-lettered and shows up in the
    /// admin view. Five attempts over the backoff below is about twenty
    /// minutes, which covers a provider blip without hiding a real fault for
    /// an afternoon.
    /// </summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Doubles each attempt, from this. 30s, 1m, 2m, 4m, 8m.</summary>
    public int BackoffSeconds { get; set; } = 30;

    /// <summary>
    /// A job claimed for longer than this is assumed to have died with its
    /// worker and is returned to the queue.
    /// </summary>
    public int StaleAfterMinutes { get; set; } = 30;

    /// <summary>Set false in tests that drive the dispatcher by hand.</summary>
    public bool Enabled { get; set; } = true;
}
