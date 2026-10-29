"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import Input from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { ToggleRow } from "@/components/ui/toggle-row";
import type { CreateReminder, ReminderSchedule } from "@/lib/api/reminders";

/** What the person can pick. The API words the summary and refuses what its rules do not allow. */
export const KIND_OPTIONS = [
  { value: "workout", label: "Workout" },
  { value: "measurement", label: "Measurements" },
  { value: "meal_log", label: "Log a meal" },
  { value: "custom", label: "Something else" },
] as const;

const REPEAT_OPTIONS = [
  { value: "daily", label: "Every day" },
  { value: "weekdays", label: "Weekdays" },
  { value: "weekends", label: "Weekends" },
  { value: "once", label: "Once" },
  { value: "hours", label: "Every few hours" },
] as const;

type Repeat = (typeof REPEAT_OPTIONS)[number]["value"];

/** The channels a reminder can go out on, by the API's names. Each still follows the person's choices for reminders. */
const CHANNEL_OPTIONS = [
  {
    value: "inApp",
    label: "In the app",
    description: "In your notifications list.",
  },
  {
    value: "telegram",
    label: "Telegram",
    description: "A message from the bot, once Telegram is linked.",
  },
  {
    value: "push",
    label: "Push",
    description: "On a phone with the app installed.",
  },
  {
    value: "email",
    label: "Email",
    description: "To your address, when email is on for reminders.",
  },
] as const;

type Channel = (typeof CHANNEL_OPTIONS)[number]["value"];

const TIMES = Array.from({ length: 48 }, (_, index) => {
  const value = `${String(Math.floor(index / 2)).padStart(2, "0")}:${index % 2 ? "30" : "00"}`;
  return { value, label: value };
});

const HOURS = ["1", "2", "3", "4", "6", "8", "12"].map((value) => ({
  value,
  label: value === "1" ? "Every hour" : `Every ${value} hours`,
}));

const LEADS = [
  { value: "30", label: "30 minutes before" },
  { value: "60", label: "1 hour before" },
  { value: "120", label: "2 hours before" },
  { value: "1440", label: "The day before" },
];

/** The picked repeat as the API's schedule. */
function schedule(
  repeat: Repeat,
  time: string,
  date: string,
  hours: string,
): ReminderSchedule {
  switch (repeat) {
    case "weekdays":
      return {
        repeat: "days",
        time,
        days: ["mon", "tue", "wed", "thu", "fri"],
      };
    case "weekends":
      return { repeat: "days", time, days: ["sat", "sun"] };
    case "once":
      return { repeat: "once", time, date: date || null };
    case "hours":
      return { repeat: "every", everyHours: Number(hours) };
    default:
      return { repeat: "daily", time };
  }
}

/**
 * The fields of a reminder, for Settings and for each "Remind me" button. A pre-cook reminder asks
 * when the prep starts and how long before; the others ask how often and when.
 */
export function ReminderForm({
  base,
  chooseKind = false,
  submitLabel = "Set reminder",
  busy,
  onSubmit,
  onCancel,
}: {
  /** What the place it was opened from already knows: the kind and what it is tied to. */
  base: CreateReminder;
  chooseKind?: boolean;
  submitLabel?: string;
  busy?: boolean;
  onSubmit: (request: CreateReminder) => void;
  onCancel?: () => void;
}) {
  const [kind, setKind] = useState(base.kind);
  const [title, setTitle] = useState(base.title ?? "");
  const [repeat, setRepeat] = useState<Repeat>("daily");
  const [time, setTime] = useState("07:00");
  const [date, setDate] = useState("");
  const [hours, setHours] = useState("2");
  const [prepTime, setPrepTime] = useState(base.prepTime ?? "18:00");
  const [lead, setLead] = useState(String(base.leadMinutes ?? 60));
  const [channels, setChannels] = useState<string[]>(
    base.channels ?? CHANNEL_OPTIONS.map((option) => option.value),
  );
  const preCook = kind === "pre_cook";

  function submit(event: React.FormEvent) {
    event.preventDefault();
    onSubmit({
      ...base,
      kind,
      title: title.trim() || null,
      channels: CHANNEL_OPTIONS.map((option) => option.value).filter(
        (value: Channel) => channels.includes(value),
      ),
      ...(preCook
        ? { prepTime, leadMinutes: Number(lead), schedule: undefined }
        : { schedule: schedule(repeat, time, date, hours) }),
    });
  }

  return (
    <form onSubmit={submit} className="space-y-4" data-testid="reminder-form">
      {chooseKind && (
        <div>
          <label
            htmlFor="reminder-kind"
            className="mb-1.5 block text-sm font-medium"
          >
            Remind me about
          </label>
          <Select
            id="reminder-kind"
            value={kind}
            onChange={setKind}
            options={KIND_OPTIONS}
            searchable={false}
          />
        </div>
      )}
      <Input
        id="reminder-title"
        label={
          kind === "custom" ? "What to remind you about" : "Title (optional)"
        }
        value={title}
        maxLength={120}
        required={kind === "custom"}
        onChange={(event) => setTitle(event.target.value)}
      />
      {preCook ? (
        <div className="grid gap-4 sm:grid-cols-2">
          <div>
            <label
              htmlFor="reminder-prep-time"
              className="mb-1.5 block text-sm font-medium"
            >
              Prep starts at
            </label>
            <Select
              id="reminder-prep-time"
              value={prepTime}
              onChange={setPrepTime}
              options={TIMES}
              searchable={false}
            />
          </div>
          <div>
            <label
              htmlFor="reminder-lead"
              className="mb-1.5 block text-sm font-medium"
            >
              Remind me
            </label>
            <Select
              id="reminder-lead"
              value={lead}
              onChange={setLead}
              options={LEADS}
              searchable={false}
            />
          </div>
        </div>
      ) : (
        <div className="grid gap-4 sm:grid-cols-2">
          <div>
            <label
              htmlFor="reminder-repeat"
              className="mb-1.5 block text-sm font-medium"
            >
              How often
            </label>
            <Select
              id="reminder-repeat"
              value={repeat}
              onChange={(next) => setRepeat(next as Repeat)}
              options={REPEAT_OPTIONS}
              searchable={false}
            />
          </div>
          {repeat === "hours" ? (
            <div>
              <label
                htmlFor="reminder-hours"
                className="mb-1.5 block text-sm font-medium"
              >
                Interval
              </label>
              <Select
                id="reminder-hours"
                value={hours}
                onChange={setHours}
                options={HOURS}
                searchable={false}
              />
            </div>
          ) : (
            <div>
              <label
                htmlFor="reminder-time"
                className="mb-1.5 block text-sm font-medium"
              >
                At
              </label>
              <Select
                id="reminder-time"
                value={time}
                onChange={setTime}
                options={TIMES}
                searchable={false}
              />
            </div>
          )}
          {repeat === "once" && (
            <Input
              id="reminder-date"
              label="On"
              type="date"
              value={date}
              required
              onChange={(event) => setDate(event.target.value)}
            />
          )}
        </div>
      )}
      <fieldset className="border-b border-border">
        <legend className="mb-1.5 text-sm font-medium">Send it to</legend>
        {CHANNEL_OPTIONS.map((option) => (
          <ToggleRow
            key={option.value}
            label={option.label}
            description={option.description}
            checked={channels.includes(option.value)}
            onChange={(on) =>
              setChannels((current) =>
                on
                  ? [...current, option.value]
                  : current.filter((value) => value !== option.value),
              )
            }
          />
        ))}
      </fieldset>
      <div className="flex flex-wrap justify-end gap-2 pt-1">
        {onCancel && (
          <Button type="button" variant="ghost" onClick={onCancel}>
            Cancel
          </Button>
        )}
        <Button type="submit" disabled={busy}>
          {busy ? "Saving…" : submitLabel}
        </Button>
      </div>
    </form>
  );
}
