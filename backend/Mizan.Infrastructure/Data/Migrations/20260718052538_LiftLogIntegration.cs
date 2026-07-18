using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mizan.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class LiftLogIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "bodyweight_kg",
                table: "workouts",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "completed_at",
                table: "workouts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "started_at",
                table: "workouts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "template_id",
                table: "workouts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "notes",
                table: "workout_exercises",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "superset_with_next",
                table: "workout_exercises",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_approved",
                table: "exercises",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "completed_at",
                table: "exercise_sets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "incline_percent",
                table: "exercise_sets",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "resistance_level",
                table: "exercise_sets",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "steps",
                table: "exercise_sets",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "content_reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    reporter_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    target_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolution_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_reports", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "follows",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    follower_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    followee_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    responded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_follows", x => x.id);
                    table.ForeignKey(
                        name: "FK_follows_users_followee_user_id",
                        column: x => x.followee_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_follows_users_follower_user_id",
                        column: x => x.follower_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    body = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    link_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_notifications_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "social_profiles",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    bio = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    avatar_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    default_publish_workouts = table.Column<bool>(type: "boolean", nullable: false),
                    share_token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_social_profiles", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_social_profiles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workout_drafts",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workout_drafts", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_workout_drafts_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workout_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    program_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    session_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_built_in = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workout_templates", x => x.id);
                    table.ForeignKey(
                        name: "FK_workout_templates_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "feed_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    workout_id = table.Column<Guid>(type: "uuid", nullable: true),
                    achievement_id = table.Column<Guid>(type: "uuid", nullable: true),
                    template_id = table.Column<Guid>(type: "uuid", nullable: true),
                    caption = table.Column<string>(type: "character varying(280)", maxLength: 280, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feed_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_feed_items_achievements_achievement_id",
                        column: x => x.achievement_id,
                        principalTable: "achievements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_feed_items_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_feed_items_workout_templates_template_id",
                        column: x => x.template_id,
                        principalTable: "workout_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_feed_items_workouts_workout_id",
                        column: x => x.workout_id,
                        principalTable: "workouts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workout_template_exercises",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exercise_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    sets = table.Column<int>(type: "integer", nullable: false),
                    reps_per_set = table.Column<int>(type: "integer", nullable: true),
                    target_weight_kg = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    rest_seconds_min = table.Column<int>(type: "integer", nullable: true),
                    rest_seconds_max = table.Column<int>(type: "integer", nullable: true),
                    rest_seconds_failure = table.Column<int>(type: "integer", nullable: true),
                    superset_with_next = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    progression_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    progression_strategy = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    progression_amount_kg = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    target_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    target_seconds = table.Column<int>(type: "integer", nullable: true),
                    target_distance_meters = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workout_template_exercises", x => x.id);
                    table.ForeignKey(
                        name: "FK_workout_template_exercises_exercises_exercise_id",
                        column: x => x.exercise_id,
                        principalTable: "exercises",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_workout_template_exercises_workout_templates_template_id",
                        column: x => x.template_id,
                        principalTable: "workout_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "feed_comments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    feed_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feed_comments", x => x.id);
                    table.ForeignKey(
                        name: "FK_feed_comments_feed_items_feed_item_id",
                        column: x => x.feed_item_id,
                        principalTable: "feed_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_feed_comments_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "feed_reactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    feed_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    emoji = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feed_reactions", x => x.id);
                    table.ForeignKey(
                        name: "FK_feed_reactions_feed_items_feed_item_id",
                        column: x => x.feed_item_id,
                        principalTable: "feed_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_feed_reactions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_workouts_template_id",
                table: "workouts",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "IX_content_reports_status_created_at",
                table: "content_reports",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_feed_comments_feed_item_id",
                table: "feed_comments",
                column: "feed_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_feed_comments_user_id",
                table: "feed_comments",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_feed_items_achievement_id",
                table: "feed_items",
                column: "achievement_id");

            migrationBuilder.CreateIndex(
                name: "IX_feed_items_template_id",
                table: "feed_items",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "IX_feed_items_user_id_created_at",
                table: "feed_items",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_feed_items_workout_id",
                table: "feed_items",
                column: "workout_id");

            migrationBuilder.CreateIndex(
                name: "IX_feed_reactions_feed_item_id_user_id_emoji",
                table: "feed_reactions",
                columns: new[] { "feed_item_id", "user_id", "emoji" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_feed_reactions_user_id",
                table: "feed_reactions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_follows_followee_user_id",
                table: "follows",
                column: "followee_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_follows_follower_user_id_followee_user_id",
                table: "follows",
                columns: new[] { "follower_user_id", "followee_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notifications_user_id_read_at_created_at",
                table: "notifications",
                columns: new[] { "user_id", "read_at", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_social_profiles_share_token",
                table: "social_profiles",
                column: "share_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workout_template_exercises_exercise_id",
                table: "workout_template_exercises",
                column: "exercise_id");

            migrationBuilder.CreateIndex(
                name: "IX_workout_template_exercises_template_id",
                table: "workout_template_exercises",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "IX_workout_templates_user_id_name",
                table: "workout_templates",
                columns: new[] { "user_id", "name" });

            migrationBuilder.Sql("""
                UPDATE audit_logs SET details = '{"redacted":true}'
                WHERE action IN ('ValidateTokenCommand', 'SendChatMessageCommand');

                WITH seed(name, category, muscle_group, equipment) AS (VALUES
                  ('Back Squat','Strength','Legs','Barbell'),('Front Squat','Strength','Legs','Barbell'),('Goblet Squat','Strength','Legs','Dumbbell'),
                  ('Bulgarian Split Squat','Strength','Legs','Dumbbell'),('Leg Press','Strength','Legs','Machine'),('Leg Extension','Strength','Legs','Machine'),
                  ('Romanian Deadlift','Strength','Hamstrings','Barbell'),('Conventional Deadlift','Strength','Back','Barbell'),('Sumo Deadlift','Strength','Legs','Barbell'),
                  ('Trap Bar Deadlift','Strength','Full Body','Trap Bar'),('Leg Curl','Strength','Hamstrings','Machine'),('Nordic Curl','Strength','Hamstrings','Bodyweight'),
                  ('Walking Lunge','Strength','Legs','Dumbbell'),('Reverse Lunge','Strength','Legs','Dumbbell'),('Step Up','Strength','Legs','Dumbbell'),
                  ('Hip Thrust','Strength','Glutes','Barbell'),('Glute Bridge','Strength','Glutes','Bodyweight'),('Cable Pull Through','Strength','Glutes','Cable'),
                  ('Standing Calf Raise','Strength','Calves','Machine'),('Seated Calf Raise','Strength','Calves','Machine'),
                  ('Bench Press','Strength','Chest','Barbell'),('Incline Bench Press','Strength','Chest','Barbell'),('Decline Bench Press','Strength','Chest','Barbell'),
                  ('Dumbbell Bench Press','Strength','Chest','Dumbbell'),('Incline Dumbbell Press','Strength','Chest','Dumbbell'),('Chest Press','Strength','Chest','Machine'),
                  ('Push Up','Strength','Chest','Bodyweight'),('Dip','Strength','Chest','Bodyweight'),('Cable Fly','Strength','Chest','Cable'),('Pec Deck','Strength','Chest','Machine'),
                  ('Overhead Press','Strength','Shoulders','Barbell'),('Dumbbell Shoulder Press','Strength','Shoulders','Dumbbell'),('Arnold Press','Strength','Shoulders','Dumbbell'),
                  ('Lateral Raise','Strength','Shoulders','Dumbbell'),('Cable Lateral Raise','Strength','Shoulders','Cable'),('Front Raise','Strength','Shoulders','Dumbbell'),
                  ('Reverse Fly','Strength','Shoulders','Dumbbell'),('Face Pull','Strength','Shoulders','Cable'),('Upright Row','Strength','Shoulders','Barbell'),
                  ('Pull Up','Strength','Back','Bodyweight'),('Chin Up','Strength','Back','Bodyweight'),('Lat Pulldown','Strength','Back','Cable'),
                  ('Barbell Row','Strength','Back','Barbell'),('Pendlay Row','Strength','Back','Barbell'),('Dumbbell Row','Strength','Back','Dumbbell'),
                  ('Seated Cable Row','Strength','Back','Cable'),('Chest Supported Row','Strength','Back','Machine'),('T-Bar Row','Strength','Back','Machine'),
                  ('Straight Arm Pulldown','Strength','Back','Cable'),('Back Extension','Strength','Back','Bodyweight'),
                  ('Barbell Curl','Strength','Arms','Barbell'),('Dumbbell Curl','Strength','Arms','Dumbbell'),('Hammer Curl','Strength','Arms','Dumbbell'),
                  ('Incline Curl','Strength','Arms','Dumbbell'),('Preacher Curl','Strength','Arms','Machine'),('Cable Curl','Strength','Arms','Cable'),
                  ('Close Grip Bench Press','Strength','Arms','Barbell'),('Triceps Pushdown','Strength','Arms','Cable'),('Overhead Triceps Extension','Strength','Arms','Cable'),
                  ('Skull Crusher','Strength','Arms','Barbell'),('Triceps Dip','Strength','Arms','Bodyweight'),('Kickback','Strength','Arms','Dumbbell'),
                  ('Plank','Strength','Core','Bodyweight'),('Side Plank','Strength','Core','Bodyweight'),('Hanging Leg Raise','Strength','Core','Bodyweight'),
                  ('Cable Crunch','Strength','Core','Cable'),('Ab Wheel Rollout','Strength','Core','Ab Wheel'),('Russian Twist','Strength','Core','Bodyweight'),
                  ('Pallof Press','Strength','Core','Cable'),('Farmer Carry','Strength','Full Body','Dumbbell'),('Kettlebell Swing','Strength','Full Body','Kettlebell'),
                  ('Power Clean','Strength','Full Body','Barbell'),('Clean and Press','Strength','Full Body','Barbell'),('Snatch','Strength','Full Body','Barbell'),
                  ('Thruster','Strength','Full Body','Barbell'),('Turkish Get Up','Strength','Full Body','Kettlebell'),
                  ('Treadmill Run','Cardio','Cardio','Treadmill'),('Outdoor Run','Cardio','Cardio','None'),('Stationary Bike','Cardio','Cardio','Bike'),
                  ('Outdoor Cycling','Cardio','Cardio','Bike'),('Rowing Machine','Cardio','Cardio','Rower'),('Elliptical','Cardio','Cardio','Elliptical'),
                  ('Stair Climber','Cardio','Cardio','Machine'),('Jump Rope','Cardio','Cardio','Jump Rope'),('Swimming','Cardio','Cardio','Pool'),
                  ('Walking','Cardio','Cardio','None'),('Hiking','Cardio','Cardio','None'),('Sled Push','Cardio','Full Body','Sled'),
                  ('Hamstring Stretch','Flexibility','Hamstrings','None'),('Quadriceps Stretch','Flexibility','Legs','None'),('Hip Flexor Stretch','Flexibility','Hips','None'),
                  ('Chest Stretch','Flexibility','Chest','None'),('Lat Stretch','Flexibility','Back','None'),('Shoulder Stretch','Flexibility','Shoulders','None'),
                  ('Calf Stretch','Flexibility','Calves','None'),('Child Pose','Flexibility','Back','None'),('Cobra Stretch','Flexibility','Core','None'),
                  ('Single Leg Balance','Balance','Legs','None'),('Bosu Squat','Balance','Legs','Bosu Ball'),('Bird Dog','Balance','Core','None'),
                  ('Dead Bug','Balance','Core','None'),('Single Leg Romanian Deadlift','Balance','Legs','Dumbbell'),('Heel to Toe Walk','Balance','Legs','None')
                )
                INSERT INTO exercises(id,name,description,category,muscle_group,equipment,is_custom,is_approved,created_at)
                SELECT md5('mizan-exercise:' || name)::uuid,name,NULL,category,muscle_group,equipment,false,true,NOW() FROM seed
                ON CONFLICT (id) DO NOTHING;

                INSERT INTO workout_templates(id,user_id,name,program_name,session_order,notes,is_built_in,sort_order,created_at,updated_at) VALUES
                  (md5('mizan-template:ss-a')::uuid,NULL,'Starting Strength A','Starting Strength',1,'Squat, bench press, deadlift',true,10,NOW(),NOW()),
                  (md5('mizan-template:ss-b')::uuid,NULL,'Starting Strength B','Starting Strength',2,'Squat, overhead press, power clean',true,11,NOW(),NOW()),
                  (md5('mizan-template:sl-a')::uuid,NULL,'StrongLifts 5x5 A','StrongLifts 5x5',1,'Squat, bench press, barbell row',true,20,NOW(),NOW()),
                  (md5('mizan-template:sl-b')::uuid,NULL,'StrongLifts 5x5 B','StrongLifts 5x5',2,'Squat, overhead press, deadlift',true,21,NOW(),NOW()),
                  (md5('mizan-template:ppl-push')::uuid,NULL,'PPL Push','Push Pull Legs',1,'Chest, shoulders, triceps',true,30,NOW(),NOW()),
                  (md5('mizan-template:ppl-pull')::uuid,NULL,'PPL Pull','Push Pull Legs',2,'Back and biceps',true,31,NOW(),NOW()),
                  (md5('mizan-template:ppl-legs')::uuid,NULL,'PPL Legs','Push Pull Legs',3,'Quads, hamstrings, glutes, calves',true,32,NOW(),NOW())
                ON CONFLICT (id) DO NOTHING;

                WITH rows(template_key, exercise_name, sort_order, sets, reps, rest_min, rest_max, progression, amount) AS (VALUES
                  ('ss-a','Back Squat',0,3,5,120,300,'IncreaseAllEvenly',2.5),('ss-a','Bench Press',1,3,5,120,300,'IncreaseAllEvenly',2.5),('ss-a','Conventional Deadlift',2,1,5,180,300,'IncreaseAllEvenly',5),
                  ('ss-b','Back Squat',0,3,5,120,300,'IncreaseAllEvenly',2.5),('ss-b','Overhead Press',1,3,5,120,300,'IncreaseAllEvenly',2.5),('ss-b','Power Clean',2,5,3,120,300,'IncreaseAllEvenly',2.5),
                  ('sl-a','Back Squat',0,5,5,90,300,'IncreaseAllEvenly',2.5),('sl-a','Bench Press',1,5,5,90,300,'IncreaseAllEvenly',2.5),('sl-a','Barbell Row',2,5,5,90,300,'IncreaseAllEvenly',2.5),
                  ('sl-b','Back Squat',0,5,5,90,300,'IncreaseAllEvenly',2.5),('sl-b','Overhead Press',1,5,5,90,300,'IncreaseAllEvenly',2.5),('sl-b','Conventional Deadlift',2,1,5,180,300,'IncreaseAllEvenly',5),
                  ('ppl-push','Bench Press',0,4,8,90,180,'IncreaseLowestSet',2.5),('ppl-push','Overhead Press',1,3,10,90,180,'IncreaseLowestSet',2.5),('ppl-push','Incline Dumbbell Press',2,3,10,60,120,'IncreaseLowestSet',2.5),('ppl-push','Lateral Raise',3,3,15,45,90,'None',0),('ppl-push','Triceps Pushdown',4,3,12,45,90,'None',0),
                  ('ppl-pull','Conventional Deadlift',0,3,5,180,300,'IncreaseAllEvenly',5),('ppl-pull','Pull Up',1,4,8,90,180,'IncreaseLowestSet',2.5),('ppl-pull','Barbell Row',2,4,8,90,180,'IncreaseLowestSet',2.5),('ppl-pull','Face Pull',3,3,15,45,90,'None',0),('ppl-pull','Dumbbell Curl',4,3,12,45,90,'None',0),
                  ('ppl-legs','Back Squat',0,4,8,120,240,'IncreaseLowestSet',2.5),('ppl-legs','Romanian Deadlift',1,3,10,90,180,'IncreaseLowestSet',2.5),('ppl-legs','Leg Press',2,3,12,60,120,'IncreaseLowestSet',5),('ppl-legs','Leg Curl',3,3,12,45,90,'None',0),('ppl-legs','Standing Calf Raise',4,4,15,45,90,'None',0)
                )
                INSERT INTO workout_template_exercises(id,template_id,exercise_id,sort_order,sets,reps_per_set,rest_seconds_min,rest_seconds_max,superset_with_next,progression_type,progression_strategy,progression_amount_kg,target_type)
                SELECT md5('mizan-template-exercise:' || template_key || ':' || exercise_name)::uuid,
                       md5('mizan-template:' || template_key)::uuid, md5('mizan-exercise:' || exercise_name)::uuid,
                       sort_order,sets,reps,rest_min,rest_max,false,progression,'all',amount,'Reps' FROM rows
                ON CONFLICT (id) DO NOTHING;

                INSERT INTO achievements(id,category,criteria_type,description,icon_url,name,points,threshold) VALUES
                  (md5('mizan-achievement:first-share')::uuid,'social','workouts_shared','Share your first workout',NULL,'First Workout Shared',20,1),
                  (md5('mizan-achievement:ten-shares')::uuid,'social','workouts_shared','Share 10 workouts',NULL,'Training Out Loud',75,10),
                  (md5('mizan-achievement:first-follower')::uuid,'social','followers_count','Gain your first follower',NULL,'First Follower',20,1),
                  (md5('mizan-achievement:ten-followers')::uuid,'social','followers_count','Gain 10 followers',NULL,'Training Circle',100,10),
                  (md5('mizan-achievement:first-pr')::uuid,'workout','pr_count','Set your first personal record',NULL,'Personal Best',25,1),
                  (md5('mizan-achievement:volume')::uuid,'workout','total_volume_kg','Lift 100000 kg total volume',NULL,'Heavy Lifter',200,100000),
                  (md5('mizan-achievement:template-ten')::uuid,'workout','template_completed_count','Complete 10 template workouts',NULL,'Program Regular',75,10),
                  (md5('mizan-achievement:reactions')::uuid,'social','reactions_given','React to 10 distinct feed items',NULL,'Hype Crew',40,10),
                  (md5('mizan-achievement:comments')::uuid,'social','comments_made','Comment on 5 distinct feed items',NULL,'Training Partner',40,5)
                ON CONFLICT (id) DO NOTHING;
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_workouts_workout_templates_template_id",
                table: "workouts",
                column: "template_id",
                principalTable: "workout_templates",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_workouts_workout_templates_template_id",
                table: "workouts");

            migrationBuilder.DropTable(
                name: "content_reports");

            migrationBuilder.DropTable(
                name: "feed_comments");

            migrationBuilder.DropTable(
                name: "feed_reactions");

            migrationBuilder.DropTable(
                name: "follows");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "social_profiles");

            migrationBuilder.DropTable(
                name: "workout_drafts");

            migrationBuilder.DropTable(
                name: "workout_template_exercises");

            migrationBuilder.DropTable(
                name: "feed_items");

            migrationBuilder.DropTable(
                name: "workout_templates");

            migrationBuilder.DropIndex(
                name: "IX_workouts_template_id",
                table: "workouts");

            migrationBuilder.DropColumn(
                name: "bodyweight_kg",
                table: "workouts");

            migrationBuilder.DropColumn(
                name: "completed_at",
                table: "workouts");

            migrationBuilder.DropColumn(
                name: "started_at",
                table: "workouts");

            migrationBuilder.DropColumn(
                name: "template_id",
                table: "workouts");

            migrationBuilder.DropColumn(
                name: "notes",
                table: "workout_exercises");

            migrationBuilder.DropColumn(
                name: "superset_with_next",
                table: "workout_exercises");

            migrationBuilder.DropColumn(
                name: "is_approved",
                table: "exercises");

            migrationBuilder.DropColumn(
                name: "completed_at",
                table: "exercise_sets");

            migrationBuilder.DropColumn(
                name: "incline_percent",
                table: "exercise_sets");

            migrationBuilder.DropColumn(
                name: "resistance_level",
                table: "exercise_sets");

            migrationBuilder.DropColumn(
                name: "steps",
                table: "exercise_sets");
        }
    }
}
