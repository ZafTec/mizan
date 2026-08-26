using FluentValidation;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Common;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Foods;
using Mizan.Domain.Entities;

namespace Mizan.Application.Commands;

public record UpdateFoodCommand : UpdateFoodRequest, IRequest<UpdateFoodResult>;

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
        RuleFor(x => x.Brand).MaximumLength(255);
        RuleFor(x => x.Barcode).MaximumLength(64);
        RuleFor(x => x.CaloriesPer100g).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ProteinPer100g).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CarbsPer100g).GreaterThanOrEqualTo(0);
        RuleFor(x => x.FatPer100g).GreaterThanOrEqualTo(0);
    }
}

public class UpdateFoodCommandHandler : IRequestHandler<UpdateFoodCommand, UpdateFoodResult>
{
    private readonly IMizanDbContext _context;
    private readonly HybridCache _cache;

    private readonly ICurrentUserService _currentUser;

    public UpdateFoodCommandHandler(IMizanDbContext context, HybridCache cache, ICurrentUserService currentUser)
    {
        _context = context;
        _cache = cache;
        _currentUser = currentUser;
    }

    public async Task<UpdateFoodResult> Handle(UpdateFoodCommand request, CancellationToken cancellationToken)
    {
        var food = await _context.Foods.FindAsync(new object[] { request.Id }, cancellationToken);

        if (food == null)
        {
            return new UpdateFoodResult { Success = false, Message = "Food not found" };
        }

        // A user may maintain the foods they created; the shared catalogue stays
        // admin-only. Without this an owned food would be uneditable by its owner.
        if (!_currentUser.IsInRole("admin") && food.UserId != _currentUser.UserId)
        {
            throw new ForbiddenAccessException("This food belongs to someone else");
        }

        food.Name = request.Name;
        food.Brand = request.Brand;
        food.Barcode = request.Barcode;
        food.CaloriesPer100g = request.CaloriesPer100g;
        food.ProteinPer100g = request.ProteinPer100g;
        food.CarbsPer100g = request.CarbsPer100g;
        food.FatPer100g = request.FatPer100g;
        food.FiberPer100g = request.FiberPer100g;
        food.SugarPer100g = request.SugarPer100g;
        food.SodiumPer100g = request.SodiumPer100g;
        food.ServingSize = request.ServingSize;
        food.ServingUnit = request.ServingUnit;
        food.IsVerified = request.IsVerified;
        food.ProteinCalorieRatio = Food.ComputeProteinCalorieRatio(request.CaloriesPer100g, request.ProteinPer100g);
        food.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        await _cache.RemoveByTagAsync(CacheTags.Foods, cancellationToken);

        return new UpdateFoodResult { Success = true, Message = "Food updated successfully" };
    }
}
