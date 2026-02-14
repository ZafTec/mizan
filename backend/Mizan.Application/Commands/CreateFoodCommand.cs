using FluentValidation;
using MediatR;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

public record CreateFoodCommand : IRequest<CreateFoodResult>
{
    public string Name { get; init; } = string.Empty;
    public string? Brand { get; init; }
    public string? Barcode { get; init; }
    public decimal CaloriesPer100g { get; init; }
    public decimal ProteinPer100g { get; init; }
    public decimal CarbsPer100g { get; init; }
    public decimal FatPer100g { get; init; }
    public decimal? FiberPer100g { get; init; }
    public decimal ServingSize { get; init; } = 100;
    public string ServingUnit { get; init; } = "g";
    public bool IsVerified { get; init; } = false;
}

public record CreateFoodResult
{
    public Guid Id { get; init; }
    public bool Success { get; init; }
    public string? Message { get; init; }
}

public class CreateFoodCommandValidator : AbstractValidator<CreateFoodCommand>
{
    public CreateFoodCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Brand).MaximumLength(255);
        RuleFor(x => x.Barcode).MaximumLength(64);
        RuleFor(x => x.CaloriesPer100g).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ProteinPer100g).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CarbsPer100g).GreaterThanOrEqualTo(0);
        RuleFor(x => x.FatPer100g).GreaterThanOrEqualTo(0);
    }
}

public class CreateFoodCommandHandler : IRequestHandler<CreateFoodCommand, CreateFoodResult>
{
    private readonly IMizanDbContext _context;
    private readonly IRedisCacheService _cache;

    public CreateFoodCommandHandler(IMizanDbContext context, IRedisCacheService cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<CreateFoodResult> Handle(CreateFoodCommand request, CancellationToken cancellationToken)
    {
        var food = new Food
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Brand = request.Brand,
            Barcode = request.Barcode,
            CaloriesPer100g = request.CaloriesPer100g,
            ProteinPer100g = request.ProteinPer100g,
            CarbsPer100g = request.CarbsPer100g,
            FatPer100g = request.FatPer100g,
            FiberPer100g = request.FiberPer100g,
            ServingSize = request.ServingSize,
            ServingUnit = request.ServingUnit,
            IsVerified = request.IsVerified,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Foods.Add(food);
        await _context.SaveChangesAsync(cancellationToken);

        await _cache.RemoveByPrefixAsync("foods:search:", cancellationToken);

        return new CreateFoodResult { Id = food.Id, Success = true, Message = "Food created successfully" };
    }
}
