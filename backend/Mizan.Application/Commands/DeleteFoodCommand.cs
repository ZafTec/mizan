using FluentValidation;
using MediatR;
using Mizan.Application.Interfaces;

namespace Mizan.Application.Commands;

public record DeleteFoodCommand : IRequest<DeleteFoodResult>
{
    public Guid Id { get; init; }
}

public record DeleteFoodResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
}

public class DeleteFoodCommandValidator : AbstractValidator<DeleteFoodCommand>
{
    public DeleteFoodCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class DeleteFoodCommandHandler : IRequestHandler<DeleteFoodCommand, DeleteFoodResult>
{
    private readonly IMizanDbContext _context;
    private readonly IRedisCacheService _cache;

    public DeleteFoodCommandHandler(IMizanDbContext context, IRedisCacheService cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<DeleteFoodResult> Handle(DeleteFoodCommand request, CancellationToken cancellationToken)
    {
        var food = await _context.Foods.FindAsync(new object[] { request.Id }, cancellationToken);

        if (food == null)
        {
            return new DeleteFoodResult { Success = false, Message = "Food not found" };
        }

        _context.Foods.Remove(food);
        await _context.SaveChangesAsync(cancellationToken);

        await _cache.RemoveByPrefixAsync("foods:search:", cancellationToken);

        return new DeleteFoodResult { Success = true, Message = "Food deleted successfully" };
    }
}
