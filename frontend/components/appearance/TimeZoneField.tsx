"use client";

import { useMemo, useState, useSyncExternalStore } from "react";
import { browserTimeZone } from "@/lib/auth-client";
import { clientApi } from "@/lib/api.client";
import { appToast } from "@/lib/toast";

function zones(): string[] {
  try {
    // Supported by every browser we target; the fallback keeps the field
    // usable rather than empty if it ever is not.
    return (
      (
        Intl as unknown as { supportedValuesOf?: (k: string) => string[] }
      ).supportedValuesOf?.("timeZone") ?? []
    );
  } catch {
    return [];
  }
}

function offsetLabel(zone: string): string {
  try {
    const parts = new Intl.DateTimeFormat("en", {
      timeZone: zone,
      timeZoneName: "shortOffset",
    }).formatToParts(new Date());
    return parts.find((p) => p.type === "timeZoneName")?.value ?? "";
  } catch {
    return "";
  }
}

type DeviceZones = {
  /** Every zone this browser knows, with its current offset. */
  zones: { zone: string; offset: string }[];
  /** The zone the device is set to, when it says. */
  detected: string | null;
};

let device: DeviceZones | null = null;

/**
 * Read once in the browser. The server knows different zone names (its ICU
 * lists Africa/Asmera where Chromium lists Africa/Asmara) and its own clock
 * and zone, so none of this is read while rendering on the server or during
 * hydration, which uses the server snapshot below.
 */
function deviceZones(): DeviceZones {
  device ??= {
    zones: zones().map((zone) => ({ zone, offset: offsetLabel(zone) })),
    detected: browserTimeZone(),
  };
  return device;
}

const noSubscription = () => () => {};

/**
 * Which midnight the user's day ends at.
 *
 * Not cosmetic: this decides when a streak resets and which day a late-night
 * meal is logged against. Someone three hours east of UTC on the old default
 * could log every single night and never advance their streak.
 */
export default function TimeZoneField({ current }: { current: string | null }) {
  // Null on the server and while hydrating, so both render the same markup.
  const local = useSyncExternalStore(noSubscription, deviceZones, () => null);
  const [chosen, setValue] = useState(current);
  const [saving, setSaving] = useState(false);
  const value = chosen ?? local?.detected ?? "UTC";
  const options = useMemo(() => {
    const known = local?.zones ?? [];
    const listed = known.some((option) => option.zone === value)
      ? known
      : [{ zone: value, offset: local ? offsetLabel(value) : "" }, ...known];
    return known.length > 0 || value === "UTC"
      ? listed
      : [...listed, { zone: "UTC", offset: local ? offsetLabel("UTC") : "" }];
  }, [local, value]);

  const detected = local?.detected ?? null;
  const mismatch = detected !== null && detected !== value;

  async function save(next: string) {
    const previous = chosen;
    setValue(next);
    setSaving(true);
    try {
      await clientApi("/api/Users/me", {
        method: "PUT",
        body: { timeZoneId: next },
      });
      appToast.success(`Your day now ends at midnight ${offsetLabel(next)}`);
    } catch (error) {
      setValue(previous);
      appToast.error(error, "Could not save your time zone");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="space-y-2">
      <label
        htmlFor="time-zone"
        className="block text-sm font-medium text-charcoal-blue-900 dark:text-charcoal-blue-100"
      >
        Time zone
      </label>
      <select
        id="time-zone"
        value={value}
        disabled={saving}
        onChange={(e) => save(e.target.value)}
        className="input w-full"
      >
        {options.map(({ zone, offset }) => (
          <option key={zone} value={zone}>
            {zone.replace(/_/g, " ")} {offset && `(${offset})`}
          </option>
        ))}
      </select>
      <p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
        Decides when your day ends — streaks reset and daily totals roll over at
        midnight here.
      </p>
      {mismatch && (
        <button
          type="button"
          onClick={() => save(detected)}
          className="text-xs text-verdigris-600 underline-offset-2 hover:underline dark:text-verdigris-400"
        >
          This device says {detected.replace(/_/g, " ")} — use that instead
        </button>
      )}
    </div>
  );
}
