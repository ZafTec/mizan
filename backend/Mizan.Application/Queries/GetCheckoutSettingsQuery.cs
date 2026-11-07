using MediatR;
using Mizan.Application.Billing;
using Mizan.Application.Common;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

/// <summary>
/// What the browser opens Paddle checkout with: the public client-side token and the environment, both runtime
/// settings (docs/ADMIN.md#runtime-settings), so neither is baked into the web build.
/// </summary>
public record GetCheckoutSettingsQuery : IRequest<CheckoutSettingsDto>;

public class GetCheckoutSettingsQueryHandler : IRequestHandler<GetCheckoutSettingsQuery, CheckoutSettingsDto>
{
    private readonly IRuntimeOptions<PaddleOptions> _paddle;

    public GetCheckoutSettingsQueryHandler(IRuntimeOptions<PaddleOptions> paddle) => _paddle = paddle;

    public async Task<CheckoutSettingsDto> Handle(GetCheckoutSettingsQuery request, CancellationToken cancellationToken)
    {
        var paddle = await _paddle.GetAsync(cancellationToken);
        var token = string.IsNullOrWhiteSpace(paddle.ClientToken) ? null : paddle.ClientToken.Trim();
        return new CheckoutSettingsDto(token, paddle.Environment);
    }
}
