using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mizan.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedAchievementBadges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The default badge set rendered from design/achievement-badges and
            // shipped with the web app under /badges. Only rows that still have
            // no picture are touched, so a badge an administrator uploaded later
            // is never overwritten by a redeploy. The 20 position-seeded ids come
            // from MizanDbContext; the rest derive from their catalogue key the
            // way CatalogSeed does.
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE achievements SET icon_url = NULL WHERE icon_url LIKE '/badges/128/%';
                """);
        }
    }
}
