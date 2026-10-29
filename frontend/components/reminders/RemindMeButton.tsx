"use client";

import { useState } from "react";
import { Button, type ButtonProps } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { ReminderForm } from "@/components/reminders/ReminderForm";
import { createReminder, type CreateReminder } from "@/lib/api/reminders";
import { appToast } from "@/lib/toast";

/**
 * "Remind me" beside a workout template, the measurements page or a prep session. It opens the
 * reminder fields with what that place already knows, and says what the API set.
 */
export function RemindMeButton({
  base,
  heading,
  label = "Remind me",
  variant = "ghost",
  size = "sm",
  className,
}: {
  base: CreateReminder;
  heading: string;
  label?: string;
  variant?: ButtonProps["variant"];
  size?: ButtonProps["size"];
  className?: string;
}) {
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);

  async function submit(request: CreateReminder) {
    setBusy(true);
    try {
      const reminder = await createReminder(request);
      appToast.success(
        `Reminder set: ${reminder.summary}`,
        reminder.nextRunText ? `First one ${reminder.nextRunText}.` : undefined,
      );
      setOpen(false);
    } catch (error) {
      appToast.error(error, "Could not set that reminder");
    } finally {
      setBusy(false);
    }
  }

  return (
    <>
      <Button
        type="button"
        variant={variant}
        size={size}
        className={className}
        onClick={() => setOpen(true)}
      >
        {label}
      </Button>
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="max-h-[90dvh] w-[calc(100%-2rem)] overflow-y-auto rounded-xl">
          <DialogHeader>
            <DialogTitle>{heading}</DialogTitle>
            <DialogDescription>
              Reminders come on the channels you chose for them in Settings, and
              wait out your quiet hours.
            </DialogDescription>
          </DialogHeader>
          {open && (
            <ReminderForm
              base={base}
              busy={busy}
              onSubmit={submit}
              onCancel={() => setOpen(false)}
            />
          )}
        </DialogContent>
      </Dialog>
    </>
  );
}
