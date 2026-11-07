---
name: mizan-admin
description: Administer Mizan as an administrator. Use when an admin asks about platform health, a user or household, a moderation or support queue, or wants to change a setting, campaign, price source or account.
---

# Administering Mizan

These tools are for administrators. They are not offered while an administrator is viewing Mizan as someone else.

## Reading

- `admin_overview` gives one `section` at a time: `overview`, `health`, `platform_usage`, `ai_usage`, `jobs`, `mail`, `social`, `achievements`.
- `admin_lookup` finds `users`, `sessions`, `relationships`, `households`, `notes` or `audit` entries. Subject `user` or `household` reads one by `id`; `include` adds its support notes or audit entries.
- `admin_inbox` lists a `queue`: `content_reports`, `price_reports`, `price_review`, `contact`, `jobs`, `mail`. `contact_item` and `mail_item` read one message.

## Everything else is an action

- `find_action` mode `search` finds actions by words, such as "campaign", "price source" or "flag". Mode `describe` gives one action's arguments. Mode `run` runs a read-only action.
- `run_action` runs a change: `action` is the id from `find_action`, and `arguments` holds what `describe` lists.
- Most changes run at once and return the result.
- A destructive change returns a preview and a token instead. Show the preview to the admin in plain words. Only when they agree, call `confirm_action` with the token. A token works once, for ten minutes, and is refused when what the preview showed has changed; run the action again for a fresh preview.
- Pausing a campaign, revoking one session and revoking all of a user's sessions are safety stops. They run at once, without a preview, and are recorded in the audit log.

## Care

- Read before you change: look the user, household or campaign up first, and name it back to the admin.
- Never change a setting that holds a secret through these tools. Send the admin to the settings page instead.
- Do not repeat personal data beyond what the admin asked for.
