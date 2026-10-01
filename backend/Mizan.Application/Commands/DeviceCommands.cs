using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

public record DeviceDto(Guid Id, string Platform, string? DeviceName, DateTime CreatedAt, DateTime LastSeenAt);

/// <summary>
/// Registers a phone for push notifications. The app calls this on every launch and
/// whenever the provider rotates the token, so it is an upsert: the same token again only
/// refreshes it. A token that was last registered by another account moves to this one,
/// because a device belongs to whoever is signed in on it now.
/// </summary>
public record RegisterDeviceCommand(string Token, string Platform, string? DeviceName) : IRequest<DeviceDto>;

public sealed class RegisterDeviceCommandValidator : AbstractValidator<RegisterDeviceCommand>
{
    public RegisterDeviceCommandValidator()
    {
        RuleFor(c => c.Token).NotEmpty().MaximumLength(1024);
        RuleFor(c => c.Platform).Must(p => p == DeviceToken.Android).WithMessage("Platform must be 'android'.");
        RuleFor(c => c.DeviceName).MaximumLength(100);
    }
}

public sealed class RegisterDeviceCommandHandler : IRequestHandler<RegisterDeviceCommand, DeviceDto>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public RegisterDeviceCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<DeviceDto> Handle(RegisterDeviceCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var now = DateTime.UtcNow;

        var device = await _context.DeviceTokens.FirstOrDefaultAsync(d => d.Token == request.Token, ct);
        if (device is null)
        {
            device = new DeviceToken { Id = Guid.NewGuid(), Token = request.Token, CreatedAt = now };
            _context.DeviceTokens.Add(device);
        }

        device.UserId = userId;
        device.Platform = request.Platform;
        device.DeviceName = request.DeviceName;
        device.LastSeenAt = now;
        await _context.SaveChangesAsync(ct);
        return new DeviceDto(device.Id, device.Platform, device.DeviceName, device.CreatedAt, device.LastSeenAt);
    }
}

public record ListDevicesQuery : IRequest<IReadOnlyList<DeviceDto>>;

public sealed class ListDevicesQueryHandler : IRequestHandler<ListDevicesQuery, IReadOnlyList<DeviceDto>>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ListDevicesQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<DeviceDto>> Handle(ListDevicesQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        return await _context.DeviceTokens.AsNoTracking()
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.LastSeenAt)
            .Select(d => new DeviceDto(d.Id, d.Platform, d.DeviceName, d.CreatedAt, d.LastSeenAt))
            .ToListAsync(ct);
    }
}

/// <summary>Called when the person signs out on the phone, so it stops receiving their notifications.</summary>
public record UnregisterDeviceCommand(Guid Id) : IRequest;

public sealed class UnregisterDeviceCommandHandler : IRequestHandler<UnregisterDeviceCommand>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UnregisterDeviceCommandHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task Handle(UnregisterDeviceCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var removed = await _context.DeviceTokens.Where(d => d.Id == request.Id && d.UserId == userId).ExecuteDeleteAsync(ct);
        if (removed == 0) throw new EntityNotFoundException("Device not found");
    }
}
