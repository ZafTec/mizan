using Microsoft.EntityFrameworkCore.Migrations;

namespace Mizan.Infrastructure.Migrations;

/// <summary>
/// Data motion for the branch migration. EF generates the schema part of a
/// migration; this file carries the row updates that go with it. The generated
/// migration calls <see cref="Before"/> first, so the rows the schema change
/// would drop are copied aside, <see cref="After"/> last, so they are written
/// back in their new shape, and <see cref="Down"/> before reverting. Every
/// statement is idempotent, so a regenerated migration can call it again
/// without harm.
/// </summary>
internal static class BranchDataMotion
{
    /// <summary>
    /// The old plan rows: one recipe per date, and a plan with an end date.
    /// Both go away in this migration, so they are copied first. Copying with
    /// IF NOT EXISTS keeps a copy made by an earlier, interrupted run.
    /// </summary>
    public static void Before(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS _legacy_meal_plan_recipes AS
                SELECT id, meal_plan_id, recipe_id, date, meal_type, servings FROM meal_plan_recipes;
                """);
        migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS _legacy_meal_plans AS
                SELECT id, start_date, end_date FROM meal_plans;
                """);
    }

    public static void After(MigrationBuilder migrationBuilder)
    {
        // The audit log used to serialize whole commands, so a sign-in,
        // registration or password change stored the password in
        // audit_logs.details. Keep the event, drop the secrets.
        migrationBuilder.Sql(@"
                UPDATE audit_logs
                SET details = CASE action
                    WHEN 'LoginCommand' THEN json_build_object('Email', details::json->>'Email', 'IpAddress', details::json->>'IpAddress', 'UserAgent', details::json->>'UserAgent')::text
                    WHEN 'RegisterCommand' THEN json_build_object('Email', details::json->>'Email', 'Name', details::json->>'Name', 'TimeZoneId', details::json->>'TimeZoneId')::text
                    WHEN 'ResetPasswordCommand' THEN '{""Reset"":true}'
                    WHEN 'ChangePasswordCommand' THEN '{""Changed"":true}'
                    WHEN 'VerifyEmailCommand' THEN '{""Verified"":true}'
                    WHEN 'ConsumeTelegramLinkCommand' THEN json_build_object('TelegramUserId', details::json->>'TelegramUserId', 'TelegramUsername', details::json->>'TelegramUsername')::text
                    WHEN 'RequestFollowCommand' THEN '{""Requested"":true}'
                END
                WHERE action IN ('LoginCommand','RegisterCommand','ResetPasswordCommand','ChangePasswordCommand','VerifyEmailCommand','ConsumeTelegramLinkCommand','RequestFollowCommand')
                  AND details IS NOT NULL;
            ");

        // Rows that existed before updated_at did get their creation time,
        // not the migration time.
        migrationBuilder.Sql("UPDATE workouts SET updated_at = created_at;");
        migrationBuilder.Sql("UPDATE food_diary_entries SET updated_at = logged_at;");
        migrationBuilder.Sql("UPDATE body_measurements SET updated_at = created_at;");

        // Every seeded achievement gets its default badge from
        // frontend/public/badges. Only rows without a picture are touched, so a
        // badge an administrator uploaded is never overwritten by a redeploy.
        migrationBuilder.Sql("""
                UPDATE achievements AS a
                SET icon_url = badge.path
                FROM (VALUES
                  ('ac000000-0000-0000-0000-000000000001'::uuid, '/badges/128/achievement-first-meal.png'),
                  ('ac000000-0000-0000-0000-000000000002'::uuid, '/badges/128/achievement-getting-started.png'),
                  ('ac000000-0000-0000-0000-000000000003'::uuid, '/badges/128/achievement-century-club.png'),
                  ('ac000000-0000-0000-0000-000000000004'::uuid, '/badges/128/achievement-half-grand.png'),
                  ('ac000000-0000-0000-0000-000000000016'::uuid, '/badges/128/achievement-kitchen-opener.png'),
                  ('ac000000-0000-0000-0000-000000000017'::uuid, '/badges/128/achievement-chefs-shelf.png'),
                  ('ac000000-0000-0000-0000-000000000005'::uuid, '/badges/128/achievement-three-day-runner.png'),
                  ('ac000000-0000-0000-0000-000000000006'::uuid, '/badges/128/achievement-one-week-warrior.png'),
                  ('ac000000-0000-0000-0000-000000000007'::uuid, '/badges/128/achievement-two-week-titan.png'),
                  ('ac000000-0000-0000-0000-000000000008'::uuid, '/badges/128/achievement-monthly-master.png'),
                  ('ac000000-0000-0000-0000-000000000009'::uuid, '/badges/128/achievement-quarter-year-habit.png'),
                  ('ac000000-0000-0000-0000-000000000010'::uuid, '/badges/128/achievement-first-rep.png'),
                  ('ac000000-0000-0000-0000-000000000011'::uuid, '/badges/128/achievement-gym-regular.png'),
                  ('ac000000-0000-0000-0000-000000000012'::uuid, '/badges/128/achievement-iron-habit.png'),
                  ('ac000000-0000-0000-0000-000000000013'::uuid, '/badges/128/achievement-workout-week.png'),
                  (md5('mizan-achievement:first-pr')::uuid, '/badges/128/achievement-first-pr.png'),
                  (md5('mizan-achievement:volume')::uuid, '/badges/128/achievement-volume.png'),
                  (md5('mizan-achievement:template-ten')::uuid, '/badges/128/achievement-template-ten.png'),
                  ('ac000000-0000-0000-0000-000000000014'::uuid, '/badges/128/achievement-baseline-set.png'),
                  ('ac000000-0000-0000-0000-000000000015'::uuid, '/badges/128/achievement-body-tracker.png'),
                  ('ac000000-0000-0000-0000-000000000018'::uuid, '/badges/128/achievement-bronze-badge.png'),
                  ('ac000000-0000-0000-0000-000000000019'::uuid, '/badges/128/achievement-silver-badge.png'),
                  ('ac000000-0000-0000-0000-000000000020'::uuid, '/badges/128/achievement-gold-badge.png'),
                  (md5('mizan-achievement:first-share')::uuid, '/badges/128/achievement-first-share.png'),
                  (md5('mizan-achievement:ten-shares')::uuid, '/badges/128/achievement-ten-shares.png'),
                  (md5('mizan-achievement:first-follower')::uuid, '/badges/128/achievement-first-follower.png'),
                  (md5('mizan-achievement:ten-followers')::uuid, '/badges/128/achievement-ten-followers.png'),
                  (md5('mizan-achievement:reactions')::uuid, '/badges/128/achievement-reactions.png'),
                  (md5('mizan-achievement:comments')::uuid, '/badges/128/achievement-comments.png')
                ) AS badge(id, path)
                WHERE a.id = badge.id AND a.icon_url IS NULL;
                """);

        // A plan's length is what its old dates spanned. A plan whose recipes
        // fell outside its dates grows to hold them, because an entry's day
        // must be inside the plan.
        migrationBuilder.Sql("""
                UPDATE meal_plans AS p
                SET duration_days = GREATEST(1, LEAST(31, l.end_date - l.start_date + 1))
                FROM _legacy_meal_plans AS l
                WHERE p.id = l.id AND l.end_date IS NOT NULL AND l.start_date IS NOT NULL;
                """);
        migrationBuilder.Sql("""
                INSERT INTO plan_entries (id, meal_plan_id, day_index, slot, kind, recipe_id, servings, sort_order)
                SELECT r.id, r.meal_plan_id, GREATEST(0, r.date - l.start_date), upper(r.meal_type), 'Recipe', r.recipe_id, r.servings, 0
                FROM _legacy_meal_plan_recipes AS r
                JOIN _legacy_meal_plans AS l ON l.id = r.meal_plan_id
                JOIN meal_plans AS p ON p.id = r.meal_plan_id
                JOIN recipes AS rc ON rc.id = r.recipe_id
                WHERE l.start_date IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM plan_entries AS e WHERE e.id = r.id);
                """);
        migrationBuilder.Sql("""
                UPDATE meal_plans AS p
                SET duration_days = GREATEST(p.duration_days, used.last_day + 1)
                FROM (SELECT meal_plan_id, max(day_index) AS last_day FROM plan_entries GROUP BY meal_plan_id) AS used
                WHERE p.id = used.meal_plan_id AND used.last_day >= p.duration_days;
                """);
        migrationBuilder.Sql("DROP TABLE IF EXISTS _legacy_meal_plan_recipes;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS _legacy_meal_plans;");
    }

    public static void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS _legacy_meal_plan_recipes;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS _legacy_meal_plans;");
        migrationBuilder.Sql("""
                UPDATE achievements SET icon_url = NULL WHERE icon_url LIKE '/badges/128/%';
                """);
    }
}
