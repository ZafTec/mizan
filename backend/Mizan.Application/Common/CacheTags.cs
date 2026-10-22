namespace Mizan.Application.Common;

// Shared tag constants for HybridCache invalidation. Keeping them in one place
// prevents drift between producers (cache writers) and invalidators.
public static class CacheTags
{
    public const string Jwks = "jwks";
    public const string Foods = "foods";

    /// <summary>The achievement catalogue - seed data, read on every log, edited a few times a year.</summary>
    public const string Achievements = "achievements";

    /// <summary>
    /// Global, like <see cref="Foods"/>: recipes are a small, infrequently
    /// written catalogue shared across users (public ones) plus each user's
    /// own. A write anywhere clears every cached recipe read rather than
    /// tracking which page could have been affected.
    /// </summary>
    public const string Recipes = "recipes";

    /// <summary>The plans and deals on sale. Public and viewer-independent; cleared by every admin catalogue write.</summary>
    public const string BillingPlans = "billing-plans";

    /// <summary>
    /// Every read that shows a price or a cost: plans, plan days and shopping
    /// lists. Global, like <see cref="Foods"/>: any stored price, accepted
    /// report or region change by an administrator clears them all.
    /// </summary>
    public const string Prices = "prices";

    /// <summary>The user's reads priced in their region. Cleared when they switch region.</summary>
    public static string PriceRegion(Guid userId) => $"price-region:{userId}";

    public static string UserStatus(Guid userId) => $"user:{userId}";
    public static string Entitlement(Guid userId) => $"entitlement:{userId}";

    /// <summary>
    /// A user's daily and range nutrition reads. Cleared by any write to that
    /// user's food diary or active goal - both feed the same totals.
    /// </summary>
    public static string Nutrition(Guid userId) => $"nutrition:{userId}";

    /// <summary>
    /// A user's meal plan listing: the plans they own and the drafts they
    /// authored. A household member's listing also carries
    /// <see cref="HouseholdPlans"/> for each household they can see, so a write
    /// to a shared plan clears every member's list.
    /// </summary>
    public static string MealPlansList(Guid userId) => $"mealplans-list:{userId}";

    /// <summary>Every listing that shows a household's plans. Cleared by any write to a plan filed under it.</summary>
    public static string HouseholdPlans(Guid householdId) => $"household-plans:{householdId}";

    /// <summary>The user's shopping list listing, like <see cref="MealPlansList"/>.</summary>
    public static string ShoppingLists(Guid userId) => $"shopping-lists:{userId}";

    /// <summary>Every listing that shows a household's shopping lists.</summary>
    public static string HouseholdShoppingLists(Guid householdId) => $"household-shopping-lists:{householdId}";

    /// <summary>One shopping list's detail, under every viewer's key. Cleared by any write to the list or its items.</summary>
    public static string ShoppingList(Guid listId) => $"shopping-list:{listId}";

    /// <summary>
    /// One meal plan's detail, however many people have it cached. A shared
    /// plan is read by the owner and every household member under a
    /// different cache key each, but they all carry this same tag, so one
    /// edit by anyone invalidates all of them.
    /// </summary>
    public static string MealPlan(Guid mealPlanId) => $"mealplan:{mealPlanId}";

    /// <summary>
    /// The user's timezone, read on every log write. Same lifetime as their
    /// status, and cleared by the same invalidator.
    /// </summary>
    public static string UserZone(Guid userId) => $"user-zone:{userId}";
}
