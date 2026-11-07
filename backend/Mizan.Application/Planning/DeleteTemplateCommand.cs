using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Domain.Planning;

namespace Mizan.Application.Planning;

/// <summary>Removes a template. Plans already made from it are untouched; they are copies.</summary>
public record DeleteTemplateCommand(Guid Id) : IRequest<Unit>;

public class DeleteTemplateCommandHandler : IRequestHandler<DeleteTemplateCommand, Unit>
{
    private readonly IMizanDbContext _context;
    private readonly PlanAccess _access;
    private readonly HybridCache _cache;

    public DeleteTemplateCommandHandler(IMizanDbContext context, PlanAccess access, HybridCache cache)
    {
        _context = context;
        _access = access;
        _cache = cache;
    }

    public async Task<Unit> Handle(DeleteTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _access.LoadEditableAsync(request.Id, cancellationToken);
        if (template.Kind != PlanKind.Template)
            throw new EntityNotFoundException("Template", request.Id);
        _context.PlanEntries.RemoveRange(template.Entries);
        _context.PrepSessions.RemoveRange(template.PrepSessions);
        _context.MealPlans.Remove(template);
        await _context.SaveChangesAsync(cancellationToken);
        await PlanAccess.InvalidateAsync(_cache, template, cancellationToken);
        return Unit.Value;
    }
}
