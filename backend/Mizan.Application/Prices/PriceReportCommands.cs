using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Common;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Prices;
using Mizan.Domain.Entities;
using Mizan.Domain.Pricing;
using Mizan.Application.Features;
using Mizan.Domain.Features;

namespace Mizan.Application.Prices;

/// <summary>GET /api/Foods/{id}/price: the price in force for the viewer's region, or an unpriced answer.</summary>
[RequiresFeature(FeatureFlags.Prices)]
public record GetFoodPriceQuery(Guid FoodId) : IRequest<FoodPriceQuoteDto>;

public class GetFoodPriceQueryHandler : IRequestHandler<GetFoodPriceQuery, FoodPriceQuoteDto>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly PriceBookReader _prices;

    public GetFoodPriceQueryHandler(IMizanDbContext context, ICurrentUserService currentUser, PriceBookReader prices)
    {
        _context = context;
        _currentUser = currentUser;
        _prices = prices;
    }

    public async Task<FoodPriceQuoteDto> Handle(GetFoodPriceQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException("User must be authenticated");
        var food = await PriceReports.ReadableFoodAsync(_context, request.FoodId, userId, cancellationToken);
        var region = await _prices.RegionForAsync(userId, cancellationToken);
        var quotes = await _prices.QuotesAsync([food], region, cancellationToken);
        if (!quotes.TryGetValue(food.Id, out var quote))
            return new FoodPriceQuoteDto { FoodId = food.Id, RegionCode = region.Code, Currency = region.Currency, Priced = false };

        return new FoodPriceQuoteDto
        {
            FoodId = food.Id,
            RegionCode = region.Code,
            Currency = region.Currency,
            Priced = true,
            PricePerGram = quote.PricePerGram,
            PricePerKg = quote.PricePerKg,
            Price = quote.Price,
            Quantity = quote.Quantity,
            Unit = quote.Unit,
            Confidence = quote.Confidence.ToString(),
            ObservedAt = quote.ObservedAt,
            SourceId = quote.SourceId,
            SourceName = quote.SourceName,
        };
    }
}

internal static class PriceReports
{
    /// <summary>A catalogue food, or the viewer's own; anything else is "not found", as on the food page.</summary>
    public static async Task<Food> ReadableFoodAsync(IMizanDbContext context, Guid foodId, Guid userId, CancellationToken cancellationToken) =>
        await context.Foods.AsNoTracking().FirstOrDefaultAsync(f => f.Id == foodId && (f.UserId == null || f.UserId == userId), cancellationToken)
        ?? throw new EntityNotFoundException("Food", foodId);

    /// <summary>The price per kilogram of a reported amount, when the unit converts for the food.</summary>
    public static decimal? PerKg(PriceReport report, Food food)
    {
        var perGram = PriceBook.PerGram(new FoodPrice { FoodId = food.Id, Price = report.SuggestedPrice, Quantity = report.Quantity, Unit = report.Unit }, food);
        return perGram is { } g ? PriceBook.PerKg(g) : null;
    }
}

/// <summary>
/// "Report a wrong price" from the food page: what the person paid, for how
/// much. The report records the price they were shown, and every
/// administrator gets a notification.
/// </summary>
[RequiresFeature(FeatureFlags.Prices)]
public record CreatePriceReportCommand(Guid FoodId, decimal SuggestedPrice, decimal Quantity, string Unit, string? Note) : IRequest<Guid>;

public class CreatePriceReportCommandValidator : AbstractValidator<CreatePriceReportCommand>
{
    public CreatePriceReportCommandValidator()
    {
        RuleFor(x => x.FoodId).NotEmpty();
        RuleFor(x => x.SuggestedPrice).GreaterThan(0).LessThan(10_000_000);
        RuleFor(x => x.Quantity).GreaterThan(0).LessThan(10_000);
        RuleFor(x => x.Unit).Must(PriceUnits.IsValid).WithMessage("The unit is g, kg, ml, l or piece.");
        RuleFor(x => x.Note).MaximumLength(1000);
    }
}

public class CreatePriceReportCommandHandler : IRequestHandler<CreatePriceReportCommand, Guid>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly PriceBookReader _prices;
    private readonly INotificationWriter _notifications;

    public CreatePriceReportCommandHandler(
        IMizanDbContext context, ICurrentUserService currentUser, PriceBookReader prices, INotificationWriter notifications)
    {
        _context = context;
        _currentUser = currentUser;
        _prices = prices;
        _notifications = notifications;
    }

    public async Task<Guid> Handle(CreatePriceReportCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException("User must be authenticated");
        var food = await PriceReports.ReadableFoodAsync(_context, request.FoodId, userId, cancellationToken);
        var region = await _prices.RegionForAsync(userId, cancellationToken);
        var quotes = await _prices.QuotesAsync([food], region, cancellationToken);
        var unit = PriceUnits.Normalize(request.Unit);

        var report = new PriceReport
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FoodId = food.Id,
            RegionCode = region.Code,
            SuggestedPrice = request.SuggestedPrice,
            Quantity = request.Quantity,
            Unit = unit,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            ShownPricePerGram = quotes.TryGetValue(food.Id, out var shown) ? shown.PricePerGram : null,
            Status = PriceReportStatus.Open,
            CreatedAt = DateTime.UtcNow,
        };
        if (PriceReports.PerKg(report, food) is null)
            throw new DomainValidationException("That quantity and unit do not convert to grams for this food.");
        _context.PriceReports.Add(report);

        var admins = await _context.Users.AsNoTracking()
            .Where(u => u.Role == "admin" && !u.Banned)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
        foreach (var admin in admins)
        {
            await _notifications.AddAsync(
                admin,
                "price_report",
                $"Price report for {food.Name}",
                $"{request.SuggestedPrice:0.##} {region.Currency} for {request.Quantity:0.###} {unit}.",
                "/admin/prices#reports",
                cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return report.Id;
    }
}

/// <summary>Price reports for review, newest first. <c>Status</c> is Open (the default), Accepted, Rejected or all.</summary>
public record GetPriceReportsQuery(string? Status = "Open", int Page = 1, int PageSize = 50) : IRequest<PagedResult<PriceReportDto>>;

public class GetPriceReportsQueryHandler : IRequestHandler<GetPriceReportsQuery, PagedResult<PriceReportDto>>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public GetPriceReportsQueryHandler(IMizanDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<PriceReportDto>> Handle(GetPriceReportsQuery request, CancellationToken cancellationToken)
    {
        PriceAdmin.Require(_currentUser);
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var query = _context.PriceReports.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Status) && !string.Equals(request.Status, "all", StringComparison.OrdinalIgnoreCase))
        {
            if (!Enum.TryParse<PriceReportStatus>(request.Status, ignoreCase: true, out var status))
                throw new DomainValidationException("Status is Open, Accepted, Rejected or all.");
            query = query.Where(r => r.Status == status);
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new { Report = r, r.Food, UserName = r.User.Name, r.Region.Currency })
            .ToListAsync(cancellationToken);

        var items = rows.Select(r => new PriceReportDto
        {
            Id = r.Report.Id,
            UserId = r.Report.UserId,
            UserName = r.UserName,
            FoodId = r.Report.FoodId,
            FoodName = r.Food.Name,
            RegionCode = r.Report.RegionCode,
            Currency = r.Currency,
            SuggestedPrice = r.Report.SuggestedPrice,
            Quantity = r.Report.Quantity,
            Unit = r.Report.Unit,
            Note = r.Report.Note,
            ShownPricePerKg = r.Report.ShownPricePerGram is { } shown ? PriceBook.PerKg(shown) : null,
            SuggestedPricePerKg = PriceReports.PerKg(r.Report, r.Food),
            Status = r.Report.Status.ToString(),
            ResolvedByUserId = r.Report.ResolvedByUserId,
            ResolvedAt = r.Report.ResolvedAt,
            ResolutionNote = r.Report.ResolutionNote,
            CreatedAt = r.Report.CreatedAt,
        }).ToList();

        return new PagedResult<PriceReportDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }
}

/// <summary>
/// Closes an open report. Accepting writes the reported amount as a Reported
/// price under the region's report source, which wins over any estimate;
/// rejecting only closes it. The reporter is told either way.
/// </summary>
public record ResolvePriceReportCommand(Guid Id, bool Accept, string? Note) : IRequest;

public class ResolvePriceReportCommandValidator : AbstractValidator<ResolvePriceReportCommand>
{
    public ResolvePriceReportCommandValidator() => RuleFor(x => x.Note).MaximumLength(1000);
}

public class ResolvePriceReportCommandHandler : IRequestHandler<ResolvePriceReportCommand>
{
    private readonly IMizanDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly PriceBookReader _prices;
    private readonly INotificationWriter _notifications;
    private readonly HybridCache _cache;

    public ResolvePriceReportCommandHandler(
        IMizanDbContext context, ICurrentUserService currentUser, PriceBookReader prices, INotificationWriter notifications, HybridCache cache)
    {
        _context = context;
        _currentUser = currentUser;
        _prices = prices;
        _notifications = notifications;
        _cache = cache;
    }

    public async Task Handle(ResolvePriceReportCommand request, CancellationToken cancellationToken)
    {
        var adminId = PriceAdmin.Require(_currentUser);
        var report = await _context.PriceReports.Include(r => r.Food)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new EntityNotFoundException("PriceReport", request.Id);
        if (report.Status != PriceReportStatus.Open)
            throw new ConflictException("This report has already been resolved.");

        var now = DateTime.UtcNow;
        report.Status = request.Accept ? PriceReportStatus.Accepted : PriceReportStatus.Rejected;
        report.ResolvedByUserId = adminId;
        report.ResolvedAt = now;
        report.ResolutionNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

        if (request.Accept)
        {
            var source = await _prices.HouseSourceAsync(report.RegionCode, PriceSourceKind.Report, cancellationToken);
            _context.FoodPrices.Add(new FoodPrice
            {
                Id = Guid.NewGuid(),
                FoodId = report.FoodId,
                RegionCode = report.RegionCode,
                SourceId = source.Id,
                Price = report.SuggestedPrice,
                Quantity = report.Quantity,
                Unit = report.Unit,
                Confidence = PriceConfidence.Reported,
                ObservedAt = report.CreatedAt,
                ReportedByUserId = report.UserId,
                CreatedAt = now,
            });
        }

        await _notifications.AddAsync(
            report.UserId,
            "price_report_resolved",
            request.Accept ? $"Your price for {report.Food.Name} was accepted" : $"Your price report for {report.Food.Name} was reviewed",
            report.ResolutionNote,
            $"/ingredients/{report.FoodId}",
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        if (request.Accept) await PriceBookReader.InvalidateAsync(_cache, cancellationToken);
    }
}
