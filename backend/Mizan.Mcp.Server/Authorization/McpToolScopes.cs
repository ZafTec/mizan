using Mizan.Contracts.Mcp;

namespace Mizan.Mcp.Server.Authorization;

/// <summary>
/// The scope each tool needs. A connected app sees and can call only the tools
/// its grant covers. A tool missing from this table is refused, and a test
/// fails if any registered tool is missing, so a new tool cannot ship open.
/// </summary>
public static class McpToolScopes
{
    private static readonly Dictionary<string, string> Map = Build();

    public static bool TryGet(string tool, out string scope) => Map.TryGetValue(tool, out scope!);

    public static IReadOnlyDictionary<string, string> All => Map;

    private static Dictionary<string, string> Build()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        void Add(string scope, params string[] tools)
        {
            foreach (var tool in tools) map.Add(tool, scope);
        }

        // Food, nutrition and goals
        Add(McpScopes.NutritionRead, "search_foods", "get_food", "get_food_diary", "get_daily_nutrition",
            "get_nutrition_range", "get_nutrition_summary", "get_current_goal", "get_goal_history", "get_goal_progress");
        Add(McpScopes.NutritionWrite, "create_food", "update_food", "delete_food", "log_day", "log_food", "log_meal",
            "log_meal_manual", "delete_meal", "create_goal", "record_goal_progress");

        // Training
        Add(McpScopes.TrainingRead, "list_exercises", "list_workouts", "get_workout", "get_workout_stats",
            "get_workout_draft", "list_workout_templates", "get_next_workout_session");
        Add(McpScopes.TrainingWrite, "create_exercise", "update_exercise", "delete_exercise", "log_workout",
            "update_workout", "delete_workout", "save_workout_draft", "delete_workout_draft",
            "create_workout_template", "update_workout_template", "duplicate_workout_template",
            "delete_workout_template");

        // Body
        Add(McpScopes.BodyRead, "list_body_measurements");
        Add(McpScopes.BodyWrite, "log_body_measurement", "delete_body_measurement");

        // Recipes
        Add(McpScopes.RecipesRead, "search_recipes", "get_recipe");
        Add(McpScopes.RecipesWrite, "promote_to_recipe", "promote_recipe_to_preparation", "update_recipe",
            "delete_recipe", "toggle_favorite_recipe", "upload_image");

        // Meal plans and shopping
        Add(McpScopes.PlanningRead, "list_meal_plans", "get_meal_plan", "list_shopping_lists", "get_shopping_list");
        Add(McpScopes.PlanningWrite, "create_meal_plan", "update_meal_plan", "delete_meal_plan",
            "add_recipe_to_meal_plan", "remove_recipe_from_meal_plan", "update_meal_plan_recipe",
            "create_shopping_list", "add_shopping_list_item", "toggle_shopping_list_item");

        // Households
        Add(McpScopes.HouseholdsRead, "list_my_households", "get_household");
        Add(McpScopes.HouseholdsWrite, "create_household", "switch_household", "invite_to_household",
            "accept_household_invitation", "decline_household_invitation", "revoke_household_invitation",
            "leave_household", "remove_household_member");

        // Profile, achievements and the AI consent settings
        Add(McpScopes.ProfileRead, "get_my_profile", "export_profile", "get_ai_consent", "list_achievements",
            "get_streak", "get_my_subscription");
        Add(McpScopes.ProfileWrite, "update_my_profile", "set_ai_consent");

        // Notifications
        Add(McpScopes.NotificationsRead, "list_notifications", "get_unread_notification_count");
        Add(McpScopes.NotificationsWrite, "mark_notification_read", "mark_all_notifications_read");

        // Social
        Add(McpScopes.SocialRead, "get_social_profile", "list_social_follows", "get_social_feed");
        Add(McpScopes.SocialWrite, "save_social_profile", "delete_social_profile", "rotate_social_share_link",
            "request_social_follow", "respond_social_follow", "remove_social_follow", "publish_workout_to_feed",
            "delete_feed_item", "react_to_feed_item", "remove_feed_reaction", "comment_on_feed_item",
            "delete_feed_comment", "report_social_content");

        // Coaching
        Add(McpScopes.TrainerRead, "list_available_trainers", "get_my_trainer", "get_my_trainer_requests",
            "list_trainer_clients", "list_trainer_pending_requests", "get_client_nutrition");
        Add(McpScopes.TrainerWrite, "send_trainer_request", "respond_to_trainer_request");

        // The assistant. Each call spends the user's daily AI allowance.
        Add(McpScopes.AiUse, "ask_ai", "suggest_meals", "analyze_food_image", "get_ai_usage", "list_ai_threads",
            "get_ai_thread", "delete_ai_thread");

        // Administration. Every admin tool needs the admin scope, which only an administrator can grant.
        Add(McpScopes.Admin,
            "admin_get_achievement", "admin_create_achievement", "admin_update_achievement",
            "admin_delete_achievement", "admin_get_achievement_analytics", "admin_get_social_analytics",
            "admin_list_content_reports", "admin_resolve_content_report", "admin_promote_exercise",
            "admin_save_builtin_workout_template", "admin_list_audit_logs", "admin_get_audit_log_facets",
            "admin_list_users", "admin_get_user", "admin_update_user", "admin_revoke_user_sessions",
            "admin_list_sessions", "admin_list_relationships", "admin_end_relationship", "admin_list_jobs",
            "admin_get_job_stats", "admin_retry_job", "admin_delete_job", "admin_list_ai_prompts",
            "admin_get_ai_prompt", "admin_create_ai_prompt_draft", "admin_update_ai_prompt_draft",
            "admin_run_ai_prompt_evals", "admin_get_ai_prompt_evals", "admin_publish_ai_prompt_version",
            "admin_get_global_ai_usage");

        return map;
    }
}
