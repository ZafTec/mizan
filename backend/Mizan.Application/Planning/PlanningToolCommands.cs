using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mizan.Application.Exceptions;
using Mizan.Application.Interfaces;
using Mizan.Contracts.Planning;

namespace Mizan.Application.Planning;

/// <summary>
/// Changes the fields of a plan that are given and keeps the rest, through
/// <see cref="UpdateMealPlanCommand"/>, which replaces them all. Rules, when given, replace the
/// plan's rules whole.
/// </summary>
public sealed record PatchMealPlanCommand(
    Guid Id,
    string? Name = null,
    string? Description = null,
    DateOnly? StartDate = null,
    int? DurationDays = null,
    PlanRulesDto? Rules = null) : IRequest<Unit>;

public sealed class PatchMealPlanCommandHandler : IRequestHandler<PatchMealPlanCommand, Unit>
{
    private readonly ISender _sender;

    public PatchMealPlanCommandHandler(ISender sender) => _sender = sender;

    public async Task<Unit> Handle(PatchMealPlanCommand request, CancellationToken cancellationToken)
    {
        var current = await _sender.Send(new GetMealPlanByIdQuery(request.Id), cancellationToken)
            ?? throw new EntityNotFoundException($"Meal plan {request.Id} was not found.");
        return await _sender.Send(new UpdateMealPlanCommand(request.Id, new UpdateMealPlanRequest
        {
            Name = request.Name ?? current.Name,
            Description = request.Description ?? current.Description,
            StartDate = request.StartDate ?? current.StartDate,
            DurationDays = request.DurationDays ?? current.DurationDays,
            Rules = request.Rules ?? current.Rules,
        }), cancellationToken);
    }
}

/// <summary>
/// Several changes to one plan's entries and prep sessions, one after another, through the
/// commands a single change uses. It is not one transaction: the receipt says what was saved,
/// what stopped the batch and what remains, so a retry sends only the rest. Every change is
/// checked before the first is made. <c>DraftOnly</c> refuses a plan that is not a draft.
/// </summary>
public sealed record EditMealPlanCommand(Guid PlanId, IReadOnlyList<MealPlanChange> Changes, bool DraftOnly = false)
    : IRequest<PlanningBatchReceipt>
{
    public const int MaxChanges = 100;

    public static readonly IReadOnlyList<string> Actions = [AddEntry, UpdateEntry, AddPrep, UpdatePrep];

    public const string AddEntry = "add_entry";
    public const string UpdateEntry = "update_entry";
    public const string AddPrep = "add_prep";
    public const string UpdatePrep = "update_prep";
}

public sealed class EditMealPlanCommandHandler : IRequestHandler<EditMealPlanCommand, PlanningBatchReceipt>
{
    private readonly ISender _sender;
    private readonly ILogger<EditMealPlanCommandHandler> _logger;

    public EditMealPlanCommandHandler(ISender sender, ILogger<EditMealPlanCommandHandler> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    public async Task<PlanningBatchReceipt> Handle(EditMealPlanCommand request, CancellationToken cancellationToken)
    {
        var changes = request.Changes ?? [];
        if (request.PlanId == Guid.Empty) throw new DomainValidationException("'planId' is required.");
        if (changes.Count == 0) throw new DomainValidationException("Give at least one change.");
        if (changes.Count > EditMealPlanCommand.MaxChanges)
            throw new DomainValidationException($"Make at most {EditMealPlanCommand.MaxChanges} changes at once.");
        for (var index = 0; index < changes.Count; index++) Check(changes[index], index, request.DraftOnly);

        return await PlanningBatch.RunAsync(
            changes.Count,
            index => changes[index].Action,
            index => ApplyAsync(request, changes[index], cancellationToken),
            "read the plan",
            _logger,
            cancellationToken);
    }

    private static void Check(MealPlanChange change, int index, bool draftOnly)
    {
        if (change is null) throw new DomainValidationException($"changes[{index}] is empty.");
        if (!EditMealPlanCommand.Actions.Contains(change.Action))
            throw new DomainValidationException($"changes[{index}].action must be one of {string.Join(", ", EditMealPlanCommand.Actions)}.");
        var entry = change.Action is EditMealPlanCommand.AddEntry or EditMealPlanCommand.UpdateEntry;
        if (entry && change.Entry is null) throw new DomainValidationException($"changes[{index}] needs the entry's day, slot and what it is.");
        // The prep commands have no draft check, so an edit held to drafts changes entries only.
        if (!entry && draftOnly)
            throw new DomainValidationException($"changes[{index}]: only a draft's entries can be changed this way; the person changes prep sessions themselves.");
        if (!entry && change.Session is null) throw new DomainValidationException($"changes[{index}] needs the session's day and title.");
        if (change.Action == EditMealPlanCommand.UpdateEntry && (change.EntryId ?? Guid.Empty) == Guid.Empty)
            throw new DomainValidationException($"changes[{index}] needs the entryId to update.");
        if (change.Action == EditMealPlanCommand.UpdatePrep && (change.SessionId ?? Guid.Empty) == Guid.Empty)
            throw new DomainValidationException($"changes[{index}] needs the sessionId to update.");
    }

    private async Task<Guid?> ApplyAsync(EditMealPlanCommand request, MealPlanChange change, CancellationToken cancellationToken)
    {
        switch (change.Action)
        {
            case EditMealPlanCommand.AddEntry:
                return (await _sender.Send(new AddPlanEntryCommand(request.PlanId, change.Entry!, request.DraftOnly), cancellationToken)).Id;
            case EditMealPlanCommand.UpdateEntry:
                await _sender.Send(new UpdatePlanEntryCommand(request.PlanId, change.EntryId!.Value, change.Entry!, request.DraftOnly), cancellationToken);
                return change.EntryId;
            case EditMealPlanCommand.AddPrep:
                return (await _sender.Send(new AddPrepSessionCommand(request.PlanId, change.Session!), cancellationToken)).Id;
            default:
                await _sender.Send(new UpdatePrepSessionCommand(request.PlanId, change.SessionId!.Value, change.Session!), cancellationToken);
                return change.SessionId;
        }
    }
}

/// <summary>
/// Adds several items to one shopping list, one after another, through
/// <see cref="AddShoppingListItemCommand"/>. Not one transaction: the receipt says what was added
/// and what remains, so a retry sends only the rest.
/// </summary>
public sealed record AddShoppingListItemsCommand(Guid ShoppingListId, IReadOnlyList<AddShoppingListItemRequest> Items)
    : IRequest<PlanningBatchReceipt>
{
    public const int MaxItems = 100;
}

public sealed class AddShoppingListItemsCommandHandler : IRequestHandler<AddShoppingListItemsCommand, PlanningBatchReceipt>
{
    private readonly ISender _sender;
    private readonly ILogger<AddShoppingListItemsCommandHandler> _logger;

    public AddShoppingListItemsCommandHandler(ISender sender, ILogger<AddShoppingListItemsCommandHandler> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    public async Task<PlanningBatchReceipt> Handle(AddShoppingListItemsCommand request, CancellationToken cancellationToken)
    {
        var items = request.Items ?? [];
        if (request.ShoppingListId == Guid.Empty) throw new DomainValidationException("'listId' is required.");
        if (items.Count == 0) throw new DomainValidationException("Give at least one item to add.");
        if (items.Count > AddShoppingListItemsCommand.MaxItems)
            throw new DomainValidationException($"Add at most {AddShoppingListItemsCommand.MaxItems} items at once.");
        for (var index = 0; index < items.Count; index++)
        {
            if (string.IsNullOrWhiteSpace(items[index]?.ItemName))
                throw new DomainValidationException($"items[{index}] needs an itemName.");
        }

        return await PlanningBatch.RunAsync(
            items.Count,
            _ => "add_item",
            async index => (Guid?)await _sender.Send(new AddShoppingListItemCommand(request.ShoppingListId, items[index]), cancellationToken),
            "read the list",
            _logger,
            cancellationToken);
    }
}

/// <summary>Runs a checked batch one change at a time and stops at the first that fails.</summary>
internal static class PlanningBatch
{
    public static async Task<PlanningBatchReceipt> RunAsync(
        int count,
        Func<int, string> actionOf,
        Func<int, Task<Guid?>> apply,
        string lookFirst,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var completed = new List<PlanningChangeDone>();
        for (var index = 0; index < count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                completed.Add(new PlanningChangeDone(index, actionOf(index), await apply(index)));
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                // A rule or a missing record refused the change before anything was saved. Anything
                // else may have failed after the save, so the caller must look before sending it again.
                var refused = error is DomainException or ValidationException;
                if (!refused) logger.LogError(error, "Change {Index} of a planning batch failed", index);
                var message = error switch
                {
                    ValidationException validation => string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)),
                    DomainException => error.Message,
                    _ => "That could not be completed.",
                };
                var from = index + (refused ? 0 : 1);
                return new PlanningBatchReceipt(
                    false,
                    completed,
                    new PlanningChangeFailed(index, actionOf(index), message, !refused),
                    Enumerable.Range(from, count - from).ToList(),
                    refused
                        ? "The completed changes are saved. Fix the failed one and send it with the remaining ones; do not send the completed ones again."
                        : $"The failed change is unconfirmed: {lookFirst} before sending it again. Send only the remaining ones; do not send the completed ones again.");
            }
        }

        return new PlanningBatchReceipt(true, completed, null, [], null);
    }
}

/// <summary>
/// What deleting a meal plan removes, for the person to see before they confirm: the plan, its
/// entries and prep sessions, and the shopping lists made from it (kept, no longer linked).
/// Counts and names only, so the same state gives the same preview.
/// </summary>
public sealed record GetMealPlanDeletePreviewQuery(Guid Id) : IRequest<MealPlanDeletePreview>;

public sealed record MealPlanDeletePreview(
    Guid Id,
    string? Name,
    string Status,
    int Entries,
    int PrepSessions,
    IReadOnlyList<string> LinkedShoppingLists);

public sealed class GetMealPlanDeletePreviewQueryHandler : IRequestHandler<GetMealPlanDeletePreviewQuery, MealPlanDeletePreview>
{
    private readonly ISender _sender;
    private readonly IMizanDbContext _context;

    public GetMealPlanDeletePreviewQueryHandler(ISender sender, IMizanDbContext context)
    {
        _sender = sender;
        _context = context;
    }

    public async Task<MealPlanDeletePreview> Handle(GetMealPlanDeletePreviewQuery request, CancellationToken cancellationToken)
    {
        // Through the plan's own read, so the preview shows only a plan the caller may see.
        var plan = await _sender.Send(new GetMealPlanByIdQuery(request.Id), cancellationToken)
            ?? throw new EntityNotFoundException($"Meal plan {request.Id} was not found.");
        var lists = await _context.ShoppingLists.AsNoTracking()
            .Where(l => l.MealPlanId == plan.Id)
            .OrderBy(l => l.Name).ThenBy(l => l.Id)
            .Select(l => l.Name ?? "Untitled list")
            .ToListAsync(cancellationToken);
        return new MealPlanDeletePreview(plan.Id, plan.Name, plan.Status, plan.EntryCount, plan.PrepSessions.Count, lists);
    }
}

/// <summary>What clearing a list's ticked items removes: the list's name and the ticked items, by name.</summary>
public sealed record GetCheckedItemsPreviewQuery(Guid ShoppingListId) : IRequest<CheckedItemsPreview>;

public sealed record CheckedItemsPreview(Guid ListId, string? Name, IReadOnlyList<string> CheckedItems);

public sealed class GetCheckedItemsPreviewQueryHandler : IRequestHandler<GetCheckedItemsPreviewQuery, CheckedItemsPreview>
{
    private readonly ISender _sender;

    public GetCheckedItemsPreviewQueryHandler(ISender sender) => _sender = sender;

    public async Task<CheckedItemsPreview> Handle(GetCheckedItemsPreviewQuery request, CancellationToken cancellationToken)
    {
        var list = await _sender.Send(new GetShoppingListQuery(request.ShoppingListId), cancellationToken)
            ?? throw new EntityNotFoundException($"Shopping list {request.ShoppingListId} was not found.");
        var ticked = list.Items
            .Where(i => i.IsChecked)
            .OrderBy(i => i.ItemName, StringComparer.Ordinal).ThenBy(i => i.Id)
            .Select(i => i.ItemName)
            .ToList();
        return new CheckedItemsPreview(list.Id, list.Name, ticked);
    }
}
