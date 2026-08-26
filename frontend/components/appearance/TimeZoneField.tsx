"use client";

import { useMemo, useState } from "react";
import { browserTimeZone } from "@/lib/auth-client";
import { clientApi } from "@/lib/api.client";
import { appToast } from "@/lib/toast";

function zones(): string[] {
	try {
		// Supported by every browser we target; the fallback keeps the field
		// usable rather than empty if it ever is not.
		return (Intl as unknown as { supportedValuesOf?: (k: string) => string[] })
			.supportedValuesOf?.("timeZone") ?? [];
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

/**
 * Which midnight the user's day ends at.
 *
 * Not cosmetic: this decides when a streak resets and which day a late-night
 * meal is logged against. Someone three hours east of UTC on the old default
 * could log every single night and never advance their streak.
 */
export default function TimeZoneField({ current }: { current: string | null }) {
	const [value, setValue] = useState(current ?? browserTimeZone() ?? "UTC");
	const [saving, setSaving] = useState(false);
	const options = useMemo(() => {
		const all = zones();
		return all.length > 0 ? all : [value, "UTC"].filter((v, i, a) => a.indexOf(v) === i);
	}, [value]);

	const detected = browserTimeZone();
	const mismatch = detected !== null && detected !== value;

	async function save(next: string) {
		const previous = value;
		setValue(next);
		setSaving(true);
		try {
			await clientApi("/api/Users/me", { method: "PUT", body: { timeZoneId: next } });
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
				{options.map((zone) => (
					<option key={zone} value={zone}>
						{zone.replace(/_/g, " ")} {offsetLabel(zone) && `(${offsetLabel(zone)})`}
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
