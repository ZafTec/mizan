using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Ai;

namespace Mizan.Application.Queries;

/// <summary>
/// The one check before a coach reads a client's area. The client must share the area with the
/// coach; when a connected app or a hosted assistant through the gateway is asking, the client must also
/// have allowed AI to read it, because the data goes to an outside model (docs/AI.md#consent). The web
/// app asks only the first.
/// </summary>
public static class ClientReadAccess
{
    public static async Task EnsureAsync(
        IDataAccessPolicy policy,
        ICurrentUserService currentUser,
        Guid trainerId,
        Guid clientId,
        DataAxis axis,
        string notShared,
        CancellationToken cancellationToken)
    {
        if (!await policy.CanReadAsync(trainerId, clientId, axis, AccessPurpose.Display, cancellationToken))
            throw new ForbiddenAccessException(notShared);

        if ((currentUser.Grant is not null || currentUser.ViaGateway)
            && !await policy.CanReadAsync(trainerId, clientId, axis, AccessPurpose.AiContext, cancellationToken))
            throw new ClientAiConsentRequiredException(AreaName(axis));
    }

    private static string AreaName(DataAxis axis) => axis switch
    {
        DataAxis.Nutrition => "nutrition",
        DataAxis.Training => "workouts",
        DataAxis.Body => "body measurements",
        _ => axis.ToString().ToLowerInvariant(),
    };
}
