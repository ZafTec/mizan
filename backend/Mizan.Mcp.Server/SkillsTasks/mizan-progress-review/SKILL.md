---
name: mizan-progress-review
description: Review how the user is doing against their goal in Mizan. Use for weekly or monthly check-ins, "am I on track", trends in intake, weight or training, and setting or updating a goal.
---

# Progress reviews

Base every statement on data from Mizan. If a number is missing, say it is missing.

## A weekly review

1. `get_nutrition` (view `goals`) for the targets and check-ins.
2. `get_nutrition` (view `range`, `from` and `to` a week apart) for daily totals. Compare average calories and protein with the goal and note the days with no log.
3. `get_training` (view `stats`, `from` and `to`) for the week.
4. `get_body_trend` for weight or other measurements over the same period.
5. `get_my_account` (section `achievements`) for the streak.

Then write a short review: what went well, one or two things to change, and a concrete next step. Keep it kind and specific.

## Trends

Use `get_nutrition` view `range` over a longer span (up to 90 days). Weight changes day to day, so talk about direction over weeks, not single days.

## Goals

- `set_goal` (action `set`) replaces the active goal. Confirm the type (the goal types are in the resource `mizan://reference/goal-types`), the targets and any target date before you call it.
- `set_goal` (action `check_in`) records a day's check-in, for example a weight.
- `get_nutrition` (view `goals`) shows the goal, the ones before it and the check-ins over time.

## Do not

- Give medical advice or diagnose. Suggest talking to a professional when something looks concerning.
- Treat an unlogged day as zero intake. It is unknown.
