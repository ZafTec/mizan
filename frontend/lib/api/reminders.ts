import { clientApi } from "@/lib/api.client";
import type { components } from "@/types/api.generated";

/** Browser calls for the person's reminders (docs/ARCHITECTURE.md#reminders). Every rule is the API's. */
type Schemas = components["schemas"];

export type Reminder = Schemas["ReminderDto"];
export type ReminderList = Schemas["ReminderListResult"];
export type ReminderSchedule = Schemas["ReminderSchedule"];
export type CreateReminder = Schemas["CreateReminderRequest"];
export type UpdateReminder = Schemas["UpdateReminderRequest"];
export type ReminderActionResult = Schemas["ReminderActionResult"];

/** A write the person may retry carries an Idempotency-Key, so a second press adds nothing. */
const retrySafe = () => ({ "Idempotency-Key": crypto.randomUUID() });

export const listReminders = () => clientApi<ReminderList>("/api/Reminders");

export const createReminder = (body: CreateReminder) =>
  clientApi<Reminder>("/api/Reminders", {
    method: "POST",
    headers: retrySafe(),
    body,
  });

export const updateReminder = (id: string, body: UpdateReminder) =>
  clientApi<Reminder>(`/api/Reminders/${id}`, {
    method: "PUT",
    headers: retrySafe(),
    body,
  });

export const deleteReminder = (id: string) =>
  clientApi<void>(`/api/Reminders/${id}`, {
    method: "DELETE",
    headers: retrySafe(),
  });
