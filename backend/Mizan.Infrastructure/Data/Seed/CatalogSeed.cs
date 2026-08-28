namespace Mizan.Infrastructure.Data.Seed;

/// <summary>
/// The exercise library, the built-in programs and the achievement catalogue.
///
/// This used to live inside the LiftLogIntegration migration. When the history
/// collapsed to a single InitialCreate (docs/REFOCUS.md §6) it had to move
/// somewhere a migration could still call it. Every statement is
/// ON CONFLICT DO NOTHING and every id is derived from a stable name, so
/// running it twice is a no-op and re-running it after adding rows backfills
/// only the new ones.
/// </summary>
public static class CatalogSeed
{
    public const string Sql = """
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
""";
}
