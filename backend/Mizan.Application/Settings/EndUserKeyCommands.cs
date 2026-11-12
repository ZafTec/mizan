using MediatR;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Admin;

namespace Mizan.Application.Settings;

/// <summary>
/// Replaces the key Mizan signs end-user tokens with. Neither key is in the command or its result,
/// so the audit row holds no key material.
/// </summary>
public record RotateEndUserSigningKeyCommand : IRequest<EndUserKeyRotationResult>;

public sealed class RotateEndUserSigningKeyCommandHandler : IRequestHandler<RotateEndUserSigningKeyCommand, EndUserKeyRotationResult>
{
    private readonly IEndUserSigningKeys _keys;
    private readonly ICurrentUserService _user;

    public RotateEndUserSigningKeyCommandHandler(IEndUserSigningKeys keys, ICurrentUserService user)
    {
        _keys = keys;
        _user = user;
    }

    public Task<EndUserKeyRotationResult> Handle(RotateEndUserSigningKeyCommand request, CancellationToken cancellationToken) =>
        _keys.RotateAsync(_user.UserId, cancellationToken);
}
