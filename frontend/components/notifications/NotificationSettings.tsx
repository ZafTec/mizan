"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import Loading from "@/components/Loading";
import { Select } from "@/components/ui/select";
import { ToggleRow } from "@/components/ui/toggle-row";
import {
  getNotificationPreferences,
  updateNotificationPreferences,
  type NotificationKindPreference,
  type NotificationPreferences,
  type NotificationPreferencesUpdate,
} from "@/lib/api/notifications";
import { useSaveStatus } from "@/lib/hooks/useSaveStatus";
import { appToast } from "@/lib/toast";

type Channel = "inApp" | "telegram" | "push" | "email";

const CHANNELS: readonly { value: Channel; label: string }[] = [
  { value: "inApp", label: "In the app" },
  { value: "telegram", label: "Telegram" },
  { value: "push", label: "Push on this device" },
  { value: "email", label: "Email" },
];

const OFF = "off";
const HOURS = Array.from({ length: 24 }, (_, hour) => {
  const time = `${String(hour).padStart(2, "0")}:00`;
  return { value: time, label: time };
});
const START_OPTIONS = [{ value: OFF, label: "Off" }, ...HOURS];

/**
 * Which kinds of notification reach which channel, and the hours they wait out.
 * One channel is shown at a time so the list stays a column of switches on a
 * phone; every change saves at once, as the rest of the page does.
 */
export function NotificationSettings({
  status,
}: {
  status: ReturnType<typeof useSaveStatus>;
}) {
  const [preferences, setPreferences] =
    useState<NotificationPreferences | null>(null);
  const [failed, setFailed] = useState(false);
  const [channel, setChannel] = useState<Channel>("inApp");

  useEffect(() => {
    let live = true;
    getNotificationPreferences()
      .then((loaded) => live && setPreferences(loaded))
      .catch((error) => {
        if (!live) return;
        setFailed(true);
        appToast.error(error, "Could not load your notification settings");
      });
    return () => {
      live = false;
    };
  }, []);

  if (!preferences) {
    return failed ? (
      <p className="py-4 text-sm text-muted-foreground">
        Notification settings could not be loaded.
      </p>
    ) : (
      <div className="flex justify-center py-8">
        <Loading />
      </div>
    );
  }

  async function save(
    body: NotificationPreferencesUpdate,
    optimistic: NotificationPreferences,
  ) {
    const previous = preferences;
    setPreferences(optimistic);
    try {
      setPreferences(
        await status.track(() => updateNotificationPreferences(body)),
      );
    } catch (error) {
      setPreferences(previous);
      appToast.error(error, "Could not save that change");
    }
  }

  function toggle(kind: NotificationKindPreference, on: boolean) {
    const current = preferences!;
    void save(
      { kinds: [{ kind: kind.kind, [channel]: on }] },
      {
        ...current,
        kinds: current.kinds.map((item) =>
          item.kind === kind.kind ? { ...item, [channel]: on } : item,
        ),
      },
    );
  }

  function quietHours(start: string, end: string | null) {
    const current = preferences!;
    if (start === OFF) {
      void save(
        { clearQuietHours: true },
        { ...current, quietHoursStart: null, quietHoursEnd: null },
      );
      return;
    }
    const until = end ?? "07:00";
    void save(
      { quietHoursStart: start, quietHoursEnd: until },
      { ...current, quietHoursStart: start, quietHoursEnd: until },
    );
  }

  const start = preferences.quietHoursStart ?? null;
  const telegramNote =
    channel !== "telegram"
      ? null
      : !preferences.telegramLinked
        ? "Telegram is not linked, so nothing is sent there yet."
        : !preferences.telegramEnabled
          ? "Notifications are switched off in the Telegram bot. Send /notifications there to turn them on."
          : null;

  return (
    <div data-testid="notification-settings">
      <div className="setting-row">
        <label htmlFor="notification-channel" className="setting-label">
          Channel
        </label>
        <Select
          id="notification-channel"
          value={channel}
          onChange={(next) => setChannel(next as Channel)}
          options={CHANNELS}
          className="max-w-sm"
        />
      </div>
      {telegramNote && (
        <p className="border-b border-border py-3 text-sm text-muted-foreground">
          {telegramNote}{" "}
          {!preferences.telegramLinked && (
            <Link href="/profile/settings/telegram" className="text-link">
              Link Telegram
            </Link>
          )}
        </p>
      )}
      <div className="border-b border-border">
        {preferences.kinds.map((kind) => (
          <ToggleRow
            key={kind.kind}
            label={kind.label}
            description={kind.description}
            checked={kind[channel]}
            onChange={(on) => toggle(kind, on)}
          />
        ))}
      </div>
      <div className="setting-row">
        <label htmlFor="quiet-start" className="setting-label">
          Quiet hours from
        </label>
        <Select
          id="quiet-start"
          value={start ?? OFF}
          onChange={(next) =>
            quietHours(next, preferences.quietHoursEnd ?? null)
          }
          options={START_OPTIONS}
          searchable={false}
          className="max-w-sm"
        />
      </div>
      {start && (
        <div className="setting-row">
          <label htmlFor="quiet-end" className="setting-label">
            Until
          </label>
          <Select
            id="quiet-end"
            value={preferences.quietHoursEnd ?? "07:00"}
            onChange={(next) => quietHours(start, next)}
            options={HOURS}
            searchable={false}
            className="max-w-sm"
          />
        </div>
      )}
      <p className="pt-3 text-sm text-muted-foreground">
        Telegram and push wait until quiet hours end, in your time zone (
        {preferences.timeZoneId}). The app still lists them.
      </p>
    </div>
  );
}
