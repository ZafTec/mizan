---
name: mizan-coaching
description: Work with a trainer and clients in Mizan. Use when the user is a trainer reviewing a client, or a client managing a trainer request.
---

# Coaching in Mizan

Trainers see only what each client chose to share. There are three separate areas: nutrition, workouts and measurements. A client can share one and not another.

## As a client

- `get_my_coach` (view `trainers`) lists trainers, and `manage_my_coach` (action `request`) asks one to coach you.
- `get_my_coach` view `mine` shows the current relationship, view `requests` the pending requests, and view `chat` the conversation with the coach.
- `manage_my_coach` action `permissions` changes what the coach may see, and action `message` writes to them.

## As a trainer

- `get_clients` view `clients` lists the people you coach, view `requests` the requests waiting for you, and view `chat` one client's conversation.
- `get_clients` view `nutrition` reads what one client ate on a date, and view `workouts` their logged workouts.
- `manage_clients` action `answer_request` accepts or declines a request, and action `message` writes to a client.
- `draft_client_plan` drafts a meal plan for a client (action `create`) or from one of your templates (action `from_template`). It waits as a draft until the client accepts or declines it.
- `save_plan_template` creates or updates one of your own templates. `delete` kind `meal_plan_template` removes one; plans drafted from it stay.
- `ask_about_client` asks Mizan's assistant about one client. It spends your own AI allowance, not the client's.

An app reads a client's area only when the client shared it with you and also allowed AI to read it. A refusal with `client_ai_consent_required` means the client shared the area but not with AI: say so, and suggest the trainer ask the client or use Mizan's web app. A `forbidden` refusal means the client has not shared the area. Do not try other tools to get around either, and do not guess what the client ate or did.

## Tone

Report facts first, then suggestions. Keep a client's data inside the conversation with the trainer.
