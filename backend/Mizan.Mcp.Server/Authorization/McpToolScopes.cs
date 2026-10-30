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
            "get_nutrition_range", "get_nutrition_summary", "get_current_goal", "get_goal_history", "get_goal_progress",
            "get_food_price");
        Add(McpScopes.NutritionWrite, "create_food", "update_food", "delete_food", "log_day", "log_food", "log_meal",
            "log_meal_manual", "delete_meal", "delete_meal_group", "create_goal", "record_goal_progress", "delete_goal", "delete_goal_progress", "report_food_price");

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
        Add(McpScopes.PlanningRead, "list_meal_plans", "get_meal_plan", "get_plan_day", "get_plan_targets", "list_templates",
            "list_shopping_lists", "get_shopping_list");
        Add(McpScopes.PlanningWrite, "create_meal_plan", "update_meal_plan", "delete_meal_plan", "set_meal_plan_status",
            "accept_meal_plan", "decline_meal_plan", "add_plan_entry", "update_plan_entry", "remove_plan_entry",
            "log_plan_entry", "add_prep_session", "update_prep_session", "remove_prep_session",
            "generate_shopping_list", "apply_template", "generate_meal_plan",
            "create_shopping_list", "update_shopping_list", "delete_shopping_list", "add_shopping_list_item",
            "update_shopping_list_item", "delete_shopping_list_item", "toggle_shopping_list_item", "clear_checked_items");

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
        Add(McpScopes.NotificationsRead, "list_notifications", "get_unread_notification_count", "get_notification_preferences", "list_reminders");
        Add(McpScopes.NotificationsWrite, "mark_notification_read", "mark_all_notifications_read", "update_notification_preferences", "create_reminder", "update_reminder", "delete_reminder");

        // Social
        Add(McpScopes.SocialRead, "get_social_profile", "list_social_follows", "get_social_feed");
        Add(McpScopes.SocialWrite, "save_social_profile", "delete_social_profile", "rotate_social_share_link",
            "request_social_follow", "respond_social_follow", "remove_social_follow", "publish_workout_to_feed",
            "delete_feed_item", "react_to_feed_item", "remove_feed_reaction", "comment_on_feed_item",
            "delete_feed_comment", "report_social_content");

        // Coaching
        Add(McpScopes.TrainerRead, "list_available_trainers", "get_my_trainer", "get_my_trainer_requests",
            "list_trainer_clients", "list_trainer_pending_requests", "get_client_nutrition");
        Add(McpScopes.TrainerWrite, "send_trainer_request", "respond_to_trainer_request", "create_plan_for_client",
            "apply_template_for_client", "create_template", "update_template", "delete_template");

        // The assistant. Each call spends the user's daily AI allowance.
        Add(McpScopes.AiUse, "ask_ai", "suggest_meals", "analyze_food_image", "get_ai_usage", "list_ai_threads",
            "get_ai_thread", "delete_ai_thread", "get_ai_conversation");

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
            "admin_get_global_ai_usage", "admin_create_template", "admin_update_template", "admin_delete_template",
            "admin_list_price_sources", "admin_run_price_source",
            "admin_list_price_review_queue", "admin_update_price_alias", "admin_accept_price_alias_suggestions",
            "admin_import_starter_foods", "admin_add_manual_price",
            "admin_list_price_reports", "admin_resolve_price_report",
            "admin_list_settings", "admin_set_setting", "admin_clear_setting", "admin_test_smtp",
            "admin_list_mail", "admin_retry_mail", "admin_send_announcement", "admin_list_contact",
            "admin_reply_contact", "admin_mark_contact_handled", "admin_get_system_health",
            "admin_list_support_notes", "admin_add_support_note");

        // Feature flags, allowance overrides and platform usage (docs/ADMIN.md).
        Add(McpScopes.Admin,
            "admin_list_feature_flags", "admin_set_feature_flag", "admin_set_feature_flag_override",
            "admin_clear_feature_flag_override", "admin_list_usage_limits", "admin_set_usage_limit",
            "admin_clear_usage_limit", "admin_get_platform_usage");

        // Notification campaigns and lifecycle messages (docs/ADMIN.md#notification-campaigns).
        Add(McpScopes.Admin,
            "admin_list_notification_campaigns", "admin_get_notification_campaign", "admin_preview_audience",
            "admin_create_notification_campaign", "admin_send_test_notification", "admin_activate_notification_campaign",
            "admin_pause_campaign", "admin_resume_campaign", "admin_cancel_campaign");

        return map;
    }
}
