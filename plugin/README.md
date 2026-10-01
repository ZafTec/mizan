# Mizan

Log meals, workouts and body measurements, plan your week, build shopping lists and review your progress, all from a conversation with Claude.

## What it adds

- The **Mizan connector**, which lets Claude read and write your Mizan data. You sign in to your own Mizan account and choose what Claude may do: which areas (nutrition, training, body, recipes, planning, households and more), read or write, and which households it may see.
- Six **skills** that tell Claude how to use the connector well:
  - `mizan-food-logging` logs meals with the right serving counts
  - `mizan-recipes` turns logged meals into reusable recipes and manages them
  - `mizan-meal-planning` plans meals and builds shopping lists
  - `mizan-training` logs and reviews workouts
  - `mizan-progress-review` runs check-ins against your goal
  - `mizan-coaching` supports a trainer and client relationship

## Use it

Install the plugin, then connect Mizan from the plugin's **Connectors** tab and approve the access you want. After that, ask in plain words: "log two eggs and toast for breakfast", "plan my dinners for the week and make the shopping list", "how is my weight trending this month".

Some tools show a small view in the chat, such as a day's nutrition or a photo of a meal to confirm before it is logged.

## Data

Claude sends what you ask it to log or look up to your Mizan account at mizan.zaftech.co, and receives your Mizan data back. The plugin stores nothing itself and holds no credentials: access is a revocable OAuth connection you manage under **Connected apps** in Mizan, and you can disconnect it at any time. The free plan allows 15 connector calls per month.

## Keeping the skills in step

The skills in `skills/` are the same files the Mizan MCP server serves over the Skills extension. They are copied from `backend/Mizan.Mcp.Server/Skills`. A test fails if the two differ, and `scripts/sync-plugin-skills.sh` copies them again.
