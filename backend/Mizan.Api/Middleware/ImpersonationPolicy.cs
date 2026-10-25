using Mizan.Api.Controllers;

namespace Mizan.Api.Middleware;

public enum ImpersonationAccess
{
    /// <summary>The administrator viewing the site as a user may write here, as the user could.</summary>
    Allowed,

    /// <summary>A write here would do lasting damage or take the account over, so the view may only look.</summary>
    Blocked,
}

/// <summary>
/// What an administrator viewing the site as a user may change. Every controller is classified here
/// by its route prefix; a test fails for one that is not. A write to a route nobody classified is
/// refused, so a new controller is closed to the view until someone decides it is safe.
/// </summary>
public static class ImpersonationPolicy
{
    public const string ErrorCode = "impersonation_restricted";

    public sealed record Classification(string Prefix, ImpersonationAccess Access);

    /// <summary>Every controller in the API, by its route prefix as the <c>[Route]</c> attribute spells it.</summary>
    public static readonly IReadOnlyDictionary<Type, Classification> Controllers = new Dictionary<Type, Classification>
    {
        // Ordinary use of the account, which is what the view is for.
        [typeof(AchievementsController)] = new("/api/Achievements", ImpersonationAccess.Allowed),
        [typeof(AiController)] = new("/api/Ai", ImpersonationAccess.Allowed),
        [typeof(BodyMeasurementsController)] = new("/api/BodyMeasurements", ImpersonationAccess.Allowed),
        [typeof(ChatController)] = new("/api/Chat", ImpersonationAccess.Allowed),
        [typeof(ExercisesController)] = new("/api/Exercises", ImpersonationAccess.Allowed),
        [typeof(FoodsController)] = new("/api/Foods", ImpersonationAccess.Allowed),
        [typeof(GoalsController)] = new("/api/Goals", ImpersonationAccess.Allowed),
        [typeof(HealthController)] = new("/health", ImpersonationAccess.Allowed),
        [typeof(HouseholdsController)] = new("/api/Households", ImpersonationAccess.Allowed),
        [typeof(MealPlansController)] = new("/api/MealPlans", ImpersonationAccess.Allowed),
        [typeof(MealsController)] = new("/api/Meals", ImpersonationAccess.Allowed),
        [typeof(NotificationsController)] = new("/api/Notifications", ImpersonationAccess.Allowed),
        [typeof(NutritionController)] = new("/api/Nutrition", ImpersonationAccess.Allowed),
        [typeof(PricesController)] = new("/api/Prices", ImpersonationAccess.Allowed),
        [typeof(ProfileController)] = new("/api/Profile", ImpersonationAccess.Allowed),
        [typeof(RecipesController)] = new("/api/Recipes", ImpersonationAccess.Allowed),
        [typeof(ShoppingListsController)] = new("/api/ShoppingLists", ImpersonationAccess.Allowed),
        [typeof(SocialController)] = new("/api/Social", ImpersonationAccess.Allowed),
        [typeof(SyncController)] = new("/api/Sync", ImpersonationAccess.Allowed),
        [typeof(TrainersController)] = new("/api/Trainers", ImpersonationAccess.Allowed),
        [typeof(UploadsController)] = new("/api/Uploads", ImpersonationAccess.Allowed),
        [typeof(UsersController)] = new("/api/Users", ImpersonationAccess.Allowed),
        [typeof(WorkoutTemplatesController)] = new("/api/WorkoutTemplates", ImpersonationAccess.Allowed),
        [typeof(WorkoutsController)] = new("/api/Workouts", ImpersonationAccess.Allowed),

        // Credentials, sessions, billing, linked services and connected apps: a support session must
        // not be able to take the account over or leave it changed in a way the user cannot see.
        [typeof(AuthController)] = new("/api/Auth", ImpersonationAccess.Blocked),
        [typeof(DevicesController)] = new("/api/Devices", ImpersonationAccess.Blocked),
        [typeof(McpConnectionsController)] = new("/api/McpConnections", ImpersonationAccess.Blocked),
        [typeof(OAuthController)] = new("/api/oauth", ImpersonationAccess.Blocked),
        [typeof(SubscriptionsController)] = new("/api/Subscriptions", ImpersonationAccess.Blocked),
        [typeof(TelegramController)] = new("/api/Telegram", ImpersonationAccess.Blocked),

        // A message to the administrators in the user's name would be one the user never wrote.
        [typeof(ContactController)] = new("/api/Contact", ImpersonationAccess.Blocked),

        // Service and administrator surfaces. The view holds no administrator role and no service key,
        // so these are closed to it anyway; saying so here keeps the map complete.
        [typeof(AdminAiPromptsController)] = new("/api/Admin/Ai/Prompts", ImpersonationAccess.Blocked),
        [typeof(AdminBillingController)] = new("/api/admin/billing", ImpersonationAccess.Blocked),
        [typeof(AdminContactController)] = new("/api/admin/contact", ImpersonationAccess.Blocked),
        [typeof(AdminFeatureFlagsController)] = new("/api/admin/feature-flags", ImpersonationAccess.Blocked),
        [typeof(AdminUsageController)] = new("/api/admin/usage", ImpersonationAccess.Blocked),
        [typeof(AdminUsageLimitsController)] = new("/api/admin/usage-limits", ImpersonationAccess.Blocked),
        [typeof(AdminJobsController)] = new("/api/Admin/Jobs", ImpersonationAccess.Blocked),
        [typeof(AdminMailController)] = new("/api/admin/mail", ImpersonationAccess.Blocked),
        [typeof(AdminPricesController)] = new("/api/admin/prices", ImpersonationAccess.Blocked),
        [typeof(AdminRelationshipsController)] = new("/api/Admin/Relationships", ImpersonationAccess.Blocked),
        [typeof(AdminSettingsController)] = new("/api/admin/settings", ImpersonationAccess.Blocked),
        [typeof(AdminSupportNotesController)] = new("/api/admin/support-notes", ImpersonationAccess.Blocked),
        [typeof(AdminUsersController)] = new("/api/admin", ImpersonationAccess.Blocked),
        [typeof(AuditLogsController)] = new("/api/AuditLogs", ImpersonationAccess.Blocked),
        [typeof(InternalPricesController)] = new("/api/internal/prices", ImpersonationAccess.Blocked),
        [typeof(McpTasksController)] = new("/api/McpTasks", ImpersonationAccess.Blocked),
        [typeof(PaddleWebhooksController)] = new("/api/webhooks/paddle", ImpersonationAccess.Blocked),
        [typeof(SocialAdminController)] = new("/api/admin/social", ImpersonationAccess.Blocked),
    };

    /// <summary>Routes that are not controllers. The chat hub negotiates with a POST.</summary>
    public static readonly IReadOnlyDictionary<string, ImpersonationAccess> Endpoints = new Dictionary<string, ImpersonationAccess>
    {
        ["/hubs/chat"] = ImpersonationAccess.Allowed,
    };

    /// <summary>Inside a blocked controller, the few requests the view itself needs.</summary>
    public static readonly IReadOnlyList<string> AlwaysAllowed =
    [
        "/api/Auth/me",
        "/api/Auth/logout",
        "/api/Auth/impersonation/stop",
    ];

    /// <summary>
    /// Refused on every method, GET included. Signing in through an outside provider would replace the
    /// session with whoever that provider names.
    /// </summary>
    public const string ExternalSignIn = "/api/Auth/external";

    public static bool IsRefused(HttpRequest request)
    {
        var path = request.Path;
        if (path.StartsWithSegments(ExternalSignIn, StringComparison.OrdinalIgnoreCase)) return true;

        if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method))
            return false;

        if (AlwaysAllowed.Any(a => path.StartsWithSegments(a, StringComparison.OrdinalIgnoreCase))) return false;

        return Classify(path) != ImpersonationAccess.Allowed;
    }

    /// <summary>The classification of the most specific prefix covering the path, or null when nothing does.</summary>
    public static ImpersonationAccess? Classify(PathString path)
    {
        var all = Controllers.Values.Select(c => (c.Prefix, c.Access))
            .Concat(Endpoints.Select(e => (Prefix: e.Key, Access: e.Value)));
        var best = all
            .Where(c => path.StartsWithSegments(c.Prefix, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(c => c.Prefix.Length)
            .FirstOrDefault();
        return best.Prefix is null ? null : best.Access;
    }
}
