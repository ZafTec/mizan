---
name: mizan-training
description: Log and review workouts in Mizan. Use when the user describes a workout, wants sets and weights recorded, wants to repeat a template, or asks how their training is going.
---

# Training in Mizan

## Log a workout

1. Find each exercise with `list_exercises` and use its id. If an exercise does not exist, offer `create_exercise` rather than renaming it to something close.
2. Call `log_workout` with `name`, `workoutDate` (YYYY-MM-DD) and `exercisesJson`.

`exercisesJson` is an array of exercises. Each has an `exerciseId`, optional `notes`, optional `supersetWithNext`, and `sets`. Each set can have `reps`, `weightKg`, `durationSeconds`, `distanceMeters`, `completed` and `completedAt`. Fill in only what the person told you.

```json
[
  { "exerciseId": "<id>", "sets": [ { "reps": 8, "weightKg": 60 }, { "reps": 8, "weightKg": 60 } ] }
]
```

Weights are in kilograms. Convert pounds and say you did.

## From a template

`list_workout_templates` shows templates. `get_next_workout_session` with a template id returns what is planned next. After the person does it, log it with `log_workout` and pass `templateId`.

## A workout in progress

`save_workout_draft` keeps a draft the web app can resume. `get_workout_draft` reads it and `delete_workout_draft` clears it. Do not log a draft as a finished workout.

## Review

`list_workouts` shows history with sets. `get_workout_stats` (optionally with `from` and `to`) gives volume, trends and muscle-group coverage. Report what the data shows. Do not invent personal records.

## Fixing a mistake

`update_workout` takes the full workout, so read it with `get_workout` first and send it back with the change. `delete_workout` removes it; confirm first.
