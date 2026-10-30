"use client";

import { useEffect, useState } from "react";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import Loading from "@/components/Loading";
import { Button } from "@/components/ui/button";
import { ToggleRow } from "@/components/ui/toggle-row";
import { ReminderForm } from "@/components/reminders/ReminderForm";
import {
  createReminder,
  deleteReminder,
  listReminders,
  updateReminder,
  type CreateReminder,
  type Reminder,
  type ReminderList,
} from "@/lib/api/reminders";
import { useSaveStatus } from "@/lib/hooks/useSaveStatus";
import { appToast } from "@/lib/toast";

/** The sentence under a reminder: the API's summary and next time, or that it is off. */
function describe(reminder: Reminder) {
  if (!reminder.enabled) return `${reminder.summary}. Off.`;
  return reminder.nextRunText
    ? `${reminder.summary}. Next: ${reminder.nextRunText}.`
    : `${reminder.summary}. Nothing left to send.`;
}

/**
 * Settings, Reminders: each reminder with a switch and Delete, and a form to add one. Every change
 * saves at once and the list shows what the API answered.
 */
export function ReminderSettings({
  status,
}: {
  status: ReturnType<typeof useSaveStatus>;
}) {
  const [list, setList] = useState<ReminderList | null>(null);
  const [failed, setFailed] = useState(false);
  const [adding, setAdding] = useState(false);
  const [busy, setBusy] = useState(false);
  const [deleting, setDeleting] = useState<Reminder | null>(null);

  async function reload() {
    setList(await listReminders());
  }

  useEffect(() => {
    let live = true;
    listReminders()
      .then((loaded) => live && setList(loaded))
      .catch((error) => {
        if (!live) return;
        setFailed(true);
        appToast.error(error, "Could not load your reminders");
      });
    return () => {
      live = false;
    };
  }, []);

  if (!list) {
    return failed ? (
      <p className="py-4 text-sm text-muted-foreground">
        Reminders could not be loaded.
      </p>
    ) : (
      <div className="flex justify-center py-8">
        <Loading />
      </div>
    );
  }

  async function toggle(reminder: Reminder, on: boolean) {
    try {
      await status.track(() => updateReminder(reminder.id, { enabled: on }));
      await reload();
    } catch (error) {
      appToast.error(
        error,
        on
          ? "Could not turn that reminder on"
          : "Could not turn that reminder off",
      );
    }
  }

  async function add(request: CreateReminder) {
    setBusy(true);
    try {
      await status.track(() => createReminder(request));
      await reload();
      setAdding(false);
    } catch (error) {
      appToast.error(error, "Could not set that reminder");
    } finally {
      setBusy(false);
    }
  }

  async function remove() {
    if (!deleting) return;
    setBusy(true);
    try {
      await status.track(() => deleteReminder(deleting.id));
      await reload();
    } finally {
      setBusy(false);
    }
  }

  return (
    <div data-testid="reminder-settings">
      <p className="pb-3 text-sm text-muted-foreground">
        {list.activeCount} of {list.activeLimit} reminders on
        {list.isPro ? "." : ". Pro keeps more on."}
      </p>
      {list.items.length === 0 ? (
        <p className="border-t border-border py-4 text-sm text-muted-foreground">
          No reminders yet. Add one here, or use Remind me on a workout
          template, the measurements page or a prep session.
        </p>
      ) : (
        <ul className="border-b border-border">
          {list.items.map((reminder) => (
            <li key={reminder.id} data-testid="reminder-row">
              <ToggleRow
                label={reminder.title}
                description={describe(reminder)}
                checked={reminder.enabled}
                onChange={(on) => void toggle(reminder, on)}
              />
              <div className="flex justify-end pb-3">
                <button
                  type="button"
                  onClick={() => setDeleting(reminder)}
                  className="rounded-md px-2 py-1 text-sm font-medium text-red-600 hover:bg-red-50 hover:text-red-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-red-500/40 dark:text-red-400 dark:hover:bg-red-950/40"
                >
                  Delete <span className="sr-only">{reminder.title}</span>
                </button>
              </div>
            </li>
          ))}
        </ul>
      )}
      <div className="pt-4">
        {adding ? (
          <ReminderForm
            base={{ kind: "workout" }}
            chooseKind
            busy={busy}
            onSubmit={(request) => void add(request)}
            onCancel={() => setAdding(false)}
          />
        ) : (
          <Button
            type="button"
            variant="outline"
            onClick={() => setAdding(true)}
          >
            Add a reminder
          </Button>
        )}
      </div>
      <ConfirmDialog
        open={deleting !== null}
        onClose={() => setDeleting(null)}
        onConfirm={remove}
        title="Delete this reminder?"
        errorFallback="Could not delete that reminder"
        description={
          deleting
            ? `"${deleting.title}" will stop and be removed. Turning it off keeps it instead.`
            : ""
        }
        confirmLabel="Delete"
        pendingLabel="Deleting…"
      />
    </div>
  );
}
