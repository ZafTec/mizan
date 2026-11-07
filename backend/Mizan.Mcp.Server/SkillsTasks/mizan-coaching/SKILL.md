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

The tools for a coach's clients are not part of this toolset yet. Say so, and point the trainer to Mizan's web app for their clients.

If a result is empty or refused for an area, the client has not shared it. Say that. Do not try other tools to get around it, and do not guess what the client ate or did.

## Tone

Report facts first, then suggestions. Keep a client's data inside the conversation with the trainer.
