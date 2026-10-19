---
name: mizan-coaching
description: Work with a trainer and clients in Mizan. Use when the user is a trainer reviewing a client, or a client managing a trainer request.
---

# Coaching in Mizan

Trainers see only what each client chose to share. There are three separate areas: nutrition, workouts and measurements. A client can share one and not another.

## As a client

- `list_available_trainers` and `send_trainer_request` to ask a trainer to coach you.
- `get_my_trainer` and `get_my_trainer_requests` show the current relationship and pending requests.

## As a trainer

- `list_trainer_pending_requests` and `respond_to_trainer_request` handle new requests.
- `list_trainer_clients` lists active clients.
- `get_client_nutrition` returns a client's nutrition for the areas they shared.

If a result is empty or refused for an area, the client has not shared it. Say that. Do not try other tools to get around it, and do not guess what the client ate or did.

## Tone

Report facts first, then suggestions. Keep a client's data inside the conversation with the trainer.
