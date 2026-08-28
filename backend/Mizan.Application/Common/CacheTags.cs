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

    public static string UserStatus(Guid userId) => $"user:{userId}";
    public static string Entitlement(Guid userId) => $"entitlement:{userId}";

    /// <summary>
    /// A user's daily and range nutrition reads. Cleared by any write to that
    /// user's food diary or active goal - both feed the same totals.
    /// </summary>
    public static string Nutrition(Guid userId) => $"nutrition:{userId}";

    /// <summary>The signed-in user's own meal plan listing (never a household member's - the list query only ever shows plans that user owns).</summary>
    public static string MealPlansList(Guid userId) => $"mealplans-list:{userId}";

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
