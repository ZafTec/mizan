using FluentValidation;
using MediatR;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Commands;

public record UpdateFoodCommand : IRequest<UpdateFoodResult>
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal CaloriesPer100g { get; init; }
    public decimal ProteinPer100g { get; init; }
    public decimal CarbsPer100g { get; init; }
    public decimal FatPer100g { get; init; }
    public decimal? FiberPer100g { get; init; }
    public decimal ServingSize { get; init; } = 100;
    public string ServingUnit { get; init; } = "g";
    public bool IsVerified { get; init; } = false;
}

public record UpdateFoodResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
}

public class UpdateFoodCommandValidator : AbstractValidator<UpdateFoodCommand>
{
    public UpdateFoodCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.CaloriesPer100g).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ProteinPer100g).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CarbsPer100g).GreaterThanOrEqualTo(0);
        RuleFor(x => x.FatPer100g).GreaterThanOrEqualTo(0);
    }
}

public class UpdateFoodCommandHandler : IRequestHandler<UpdateFoodCommand, UpdateFoodResult>
{
    private readonly IMizanDbContext _context;
    private readonly IRedisCacheService _cache;

    public UpdateFoodCommandHandler(IMizanDbContext context, IRedisCacheService cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<UpdateFoodResult> Handle(UpdateFoodCommand request, CancellationToken cancellationToken)
    {
        var food = await _context.Foods.FindAsync(new object[] { request.Id }, cancellationToken);

        if (food == null)
        {
            return new UpdateFoodResult { Success = false, Message = "Food not found" };
        }

        food.Name = request.Name;
        food.CaloriesPer100g = request.CaloriesPer100g;
        food.ProteinPer100g = request.ProteinPer100g;
        food.CarbsPer100g = request.CarbsPer100g;
        food.FatPer100g = request.FatPer100g;
        food.FiberPer100g = request.FiberPer100g;
        food.ServingSize = request.ServingSize;
        food.ServingUnit = request.ServingUnit;
        food.IsVerified = request.IsVerified;
        food.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        await _cache.RemoveByPrefixAsync("foods:search:", cancellationToken);

        return new UpdateFoodResult { Success = true, Message = "Food updated successfully" };
    }
}
