---
name: mizan-training
description: Log and review workouts in Mizan. Use when the user describes a workout, wants sets and weights recorded, wants to repeat a template, or asks how their training is going.
---

# Training in Mizan

## Log a workout

1. Find each exercise with `get_training` (view `exercises`, `query`) and use its id. If an exercise does not exist, offer to add it with `save_workout_template` (kind `exercise`) rather than renaming it to something close.
2. Call `log_workout` (action `log`) with `name`, `date` (YYYY-MM-DD) and `exercises`.

Each exercise has an `exerciseId`, optional `notes`, optional `supersetWithNext`, and `sets`. Each set can have `reps`, `weightKg`, `durationSeconds`, `distanceMeters`, `completed` and `completedAt`. Fill in only what the person told you.

```json
{ "name": "Push day", "date": "2026-10-30", "exercises": [
  { "exerciseId": "<id>", "sets": [ { "reps": 8, "weightKg": 60 }, { "reps": 8, "weightKg": 60 } ] }
] }
```

Weights are in kilograms. Convert pounds and say you did.

## From a template

`get_training` (view `templates`) shows templates. View `next_session` with a `templateId` returns what is planned next, with the weights progression sets. After the person does it, log it with `log_workout` and pass `templateId`. Templates are made or changed with `save_workout_template` (kind `template`).

## A workout in progress

`log_workout` (action `draft`) keeps a draft the web app can resume. `get_training` (view `draft`) reads it and `delete` (kind `workout_draft`) clears it. Do not log a draft as a finished workout.

## Review

`get_training` (view `history`) shows workouts with their sets. View `stats` (optionally with `from` and `to`) gives volume, trends, personal records and muscle-group coverage. Report what the data shows. Do not invent personal records.

## Fixing a mistake

`log_workout` (action `update`, `workoutId`) replaces every exercise with those sent, so read the workout with `get_training` (view `history`, `workoutId`) first and send it back with the change. `delete` (kind `workout`) removes it; confirm with the person first.
