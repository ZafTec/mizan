---
name: mizan-progress-review
description: Review how the user is doing against their goal in Mizan. Use for weekly or monthly check-ins, "am I on track", trends in intake, weight or training, and setting or updating a goal.
---

# Progress reviews

Base every statement on data from Mizan. If a number is missing, say it is missing.

## A weekly review

1. `get_current_goal` for targets.
2. `get_nutrition_range` with `days` set to 7 for daily totals. Compare average calories and protein with the goal and note the days with no log.
3. `get_workout_stats` with `from` and `to` for the week.
4. `list_body_measurements` for weight or other measurements over the same period.
5. `get_streak` for consistency.

Then write a short review: what went well, one or two things to change, and a concrete next step. Keep it kind and specific.

## Trends

Use `get_nutrition_range` with a longer `days` (up to 90). Weight changes day to day, so talk about direction over weeks, not single days.

## Goals

- `create_goal` replaces the active goal. Confirm the type (weight_loss, muscle_gain, maintenance or custom), the targets and any target date before you call it.
- `record_goal_progress` adds a check-in, for example a weight.
- `get_goal_progress` and `get_goal_history` show how it went over time.

## Do not

- Give medical advice or diagnose. Suggest talking to a professional when something looks concerning.
- Treat an unlogged day as zero intake. It is unknown.
