using System.Text.Json;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Infrastructure.Outbox;

public record EvalRunJob(Guid VersionId, Guid AdminUserId);

/// <summary>
/// A prompt's eval suite, off the request thread.
///
/// Twenty-odd sequential provider calls will time out an HTTP request long
/// before they finish, so the console queues this and polls the matrix as
/// results land.
/// </summary>
public class EvalRunJobHandler : IOutboxHandler
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IAiEvalRunner _runner;

    public EvalRunJobHandler(IAiEvalRunner runner) => _runner = runner;

    public string Type => OutboxJobTypes.EvalRun;

    /// <summary>
    /// One. Each case is a metered provider call against a shared global
    /// ceiling, and two suites racing would burn it twice as fast for no gain.
    /// </summary>
    public int Concurrency => 1;

    public async Task HandleAsync(string payload, CancellationToken cancellationToken)
    {
        EvalRunJob job;
        try
        {
            job = JsonSerializer.Deserialize<EvalRunJob>(payload, Json)
                ?? throw new OutboxPermanentException("The queued eval run was empty.");
        }
        catch (JsonException ex)
        {
            throw new OutboxPermanentException("The queued eval run could not be read.", ex);
        }


        try
        {
            await _runner.RunAsync(job.VersionId, job.AdminUserId, cancellationToken);
        }
        catch (EntityNotFoundException ex)
        {
            // The draft was deleted while the job waited. Retrying will not
            // bring it back.
            throw new OutboxPermanentException(ex.Message, ex);
        }
        catch (AiQuotaExceededException ex)
        {
            // The eval budget is spent for the day. This one is worth
            // retrying - the backoff will not outlast a daily window, so it
            // dead-letters and an admin re-runs it tomorrow rather than the
            // queue quietly holding a job for eighteen hours.
            throw new OutboxPermanentException(ex.Message, ex);
        }
    }
}
