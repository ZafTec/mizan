using FluentValidation;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Planning;
using Mizan.Domain.Planning;

namespace Mizan.Application.Planning;

/// <summary>Changes a template's name, length and rules. Entries and sessions use the plan entry routes with the template's id.</summary>
public record UpdateTemplateCommand(Guid Id, UpdateTemplateRequest Template) : IRequest<Unit>;

public class UpdateTemplateCommandValidator : AbstractValidator<UpdateTemplateCommand>
{
    public UpdateTemplateCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Template.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Template.Description).MaximumLength(2000);
        RuleFor(x => x.Template.DurationDays).InclusiveBetween(1, 31).WithMessage("A template runs for 1 to 31 days.");
        RuleFor(x => x.Template.Rules!).SetValidator(new PlanRulesDtoValidator()).When(x => x.Template.Rules is not null);
    }
}

public class UpdateTemplateCommandHandler : IRequestHandler<UpdateTemplateCommand, Unit>
{
    private readonly IMizanDbContext _context;
    private readonly PlanAccess _access;
    private readonly HybridCache _cache;

    public UpdateTemplateCommandHandler(IMizanDbContext context, PlanAccess access, HybridCache cache)
    {
        _context = context;
        _access = access;
        _cache = cache;
    }

    public async Task<Unit> Handle(UpdateTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _access.LoadEditableAsync(request.Id, cancellationToken);
        if (template.Kind != PlanKind.Template)
            throw new EntityNotFoundException("Template", request.Id);
        PlanResize.EnsureFits(template, request.Template.DurationDays);

        template.Name = request.Template.Name.Trim();
        template.Description = string.IsNullOrWhiteSpace(request.Template.Description) ? null : request.Template.Description.Trim();
        template.DurationDays = request.Template.DurationDays;
        if (request.Template.Rules is not null) template.Rules = PlanRulesMapping.ToDomain(request.Template.Rules);
        template.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        await PlanAccess.InvalidateAsync(_cache, template, cancellationToken);
        return Unit.Value;
    }
}
