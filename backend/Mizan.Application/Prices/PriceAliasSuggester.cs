using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Mizan.Application.Interfaces;
using Mizan.Domain.Entities;
using Mizan.Domain.Planning;
using Mizan.Domain.Pricing;

namespace Mizan.Application.Prices;

/// <summary>What the review queue suggests for one product: a shared food and the package size, each when found.</summary>
public sealed record PriceAliasSuggestion(Guid? FoodId, string? FoodName, decimal? Quantity, string? Unit);

/// <summary>
/// Suggests a mapping for products in the price review queue. The package size comes from the
/// product name (<see cref="PackageSize"/>); the food is the shared catalogue food whose name the
/// rest of the product name matches by whole words (<see cref="FoodNameMatch"/>), as the planner
/// resolves a written food. The queue shows the suggestion, and accepting it writes the same
/// mapping an administrator would.
/// </summary>
public sealed class PriceAliasSuggester
{
    /// <summary>The candidate foods read for one batch, at most.</summary>
    private const int MaxCandidates = 2000;

    private readonly IMizanDbContext _context;

    public PriceAliasSuggester(IMizanDbContext context) => _context = context;

    public async Task<IReadOnlyDictionary<Guid, PriceAliasSuggestion>> SuggestAsync(
        IReadOnlyCollection<(Guid Id, string ProductName)> products, CancellationToken cancellationToken)
    {
        var parsed = products.ToDictionary(
            p => p.Id,
            p =>
            {
                var size = PackageSize.Parse(p.ProductName);
                return (Size: size, Name: size?.Name ?? p.ProductName);
            });

        var words = parsed.Values
            .SelectMany(p => FoodNameMatch.Words(p.Name))
            .Where(w => w.Length >= FoodNameMatch.MinimumContainedLength && !w.All(char.IsDigit))
            .Distinct()
            .Take(200)
            .ToList();

        List<Food> candidates = [];
        if (words.Count > 0)
        {
            candidates = await _context.Foods.AsNoTracking()
                .Where(f => f.UserId == null)
                .Where(NameContainsAny(words))
                .OrderByDescending(f => f.IsVerified).ThenBy(f => f.Name.Length)
                .Take(MaxCandidates)
                .ToListAsync(cancellationToken);
        }

        var suggestions = new Dictionary<Guid, PriceAliasSuggestion>();
        foreach (var (id, (size, name)) in parsed)
        {
            var food = FoodNameMatch.Pick(name, candidates, f => f.Name);
            suggestions[id] = new PriceAliasSuggestion(food?.Id, food?.Name, size?.Quantity, size?.Unit);
        }
        return suggestions;
    }

    /// <summary><c>f.Name.ToLower().Contains(w1) || ...</c>, one SQL query for the whole batch.</summary>
    private static Expression<Func<Food, bool>> NameContainsAny(IReadOnlyList<string> words)
    {
        var food = Expression.Parameter(typeof(Food), "f");
        var lower = Expression.Call(Expression.Property(food, nameof(Food.Name)), typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!);
        var contains = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;
        Expression? body = null;
        foreach (var word in words)
        {
            Expression<Func<string>> value = () => word;
            var test = Expression.Call(lower, contains, value.Body);
            body = body is null ? test : Expression.OrElse(body, test);
        }
        return Expression.Lambda<Func<Food, bool>>(body ?? Expression.Constant(false), food);
    }
}
