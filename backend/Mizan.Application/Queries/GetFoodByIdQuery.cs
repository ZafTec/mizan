using MediatR;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Queries;

public record GetFoodByIdQuery(Guid Id) : IRequest<FoodDto?>;

public class GetFoodByIdQueryHandler : IRequestHandler<GetFoodByIdQuery, FoodDto?>
{
    private readonly IMizanDbContext _context;

    public GetFoodByIdQueryHandler(IMizanDbContext context)
    {
        _context = context;
    }

    public async Task<FoodDto?> Handle(GetFoodByIdQuery request, CancellationToken cancellationToken)
    {
        var food = await _context.Foods
            .FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken);

        if (food == null)
            return null;

        return new FoodDto
        {
            Id = food.Id,
            Name = food.Name,
            Brand = food.Brand,
            Barcode = food.Barcode,
            ServingSize = food.ServingSize,
            ServingUnit = food.ServingUnit,
            CaloriesPer100g = food.CaloriesPer100g,
            ProteinPer100g = food.ProteinPer100g,
            CarbsPer100g = food.CarbsPer100g,
            FatPer100g = food.FatPer100g,
            FiberPer100g = food.FiberPer100g,
            IsVerified = food.IsVerified
        };
    }
}
