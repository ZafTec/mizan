"use client";

import { useEffect, useMemo, useState, useSyncExternalStore } from "react";
import { useRouter } from "next/navigation";
import { Select } from "@/components/ui/select";
import { useDebounce } from "@/lib/hooks/useDebounce";
import {
  createCampaign,
  previewCampaignAudience,
  previewCampaignMessage,
  updateCampaign,
  type CampaignAudiencePreview,
  type CampaignPreview,
  type NotificationCampaign,
} from "@/lib/api/admin-notifications";
import {
  CAMPAIGN_LIMITS,
  CHANNELS,
  DAYS,
  EMPTY_CAMPAIGN,
  EVENTS,
  PLANS,
  SENDS,
  TRISTATE,
  campaignSchema,
  formOf,
  requestOf,
  type CampaignForm,
  type Tristate,
} from "@/lib/admin-notifications/schema";
import { fieldErrors } from "@/lib/admin-ops/schema";
import { appToast, getErrorMessage } from "@/lib/toast";
import ChannelPreviews from "./ChannelPreviews";

const errorText = "mt-1 text-sm text-burnt-peach-700 dark:text-burnt-peach-400";

let zoneCache: { value: string; label: string }[] | null = null;
const NO_ZONES: { value: string; label: string }[] = [
  { value: "UTC", label: "UTC" },
];
function zoneOptions() {
  if (zoneCache) return zoneCache;
  try {
    const all =
      (
        Intl as unknown as { supportedValuesOf?: (k: string) => string[] }
      ).supportedValuesOf?.("timeZone") ?? [];
    zoneCache = [
      { value: "UTC", label: "UTC" },
      ...all.filter((z) => z !== "UTC").map((z) => ({ value: z, label: z })),
    ];
  } catch {
    zoneCache = NO_ZONES;
  }
  return zoneCache;
}
const noSubscription = () => () => {};

/**
 * Writes a campaign or a lifecycle message: what it says, who it reaches,
 * when and where. The right column shows each channel's rendering and the
 * audience count as the administrator types, both from the API. Saving keeps
 * it a draft; it is started from its own page after a test and a confirmation.
 */
export default function CampaignEditor({
  campaign,
}: {
  campaign?: NotificationCampaign;
}) {
  const router = useRouter();
  const [form, setForm] = useState<CampaignForm>(
    campaign ? formOf(campaign) : EMPTY_CAMPAIGN,
  );
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  const [preview, setPreview] = useState<CampaignPreview | null>(null);
  const [audience, setAudience] = useState<CampaignAudiencePreview | null>(
    null,
  );
  const [audienceError, setAudienceError] = useState<string | null>(null);
  const zones = useSyncExternalStore(
    noSubscription,
    zoneOptions,
    () => NO_ZONES,
  );

  const set = <K extends keyof CampaignForm>(key: K, value: CampaignForm[K]) =>
    setForm((current) => ({ ...current, [key]: value }));
  const toggle = (key: "channels" | "plans" | "days", value: string) =>
    setForm((current) => ({
      ...current,
      [key]: current[key].includes(value)
        ? current[key].filter((v) => v !== value)
        : [...current[key], value],
    }));

  const request = useMemo(() => requestOf(form), [form]);
  const messageKey = useDebounce(
    JSON.stringify({
      purpose: request.purpose,
      message: request.message,
      channels: request.channels,
    }),
    400,
  );
  const audienceKey = useDebounce(
    JSON.stringify({
      purpose: request.purpose,
      audience: request.audience,
      channels: request.channels,
    }),
    500,
  );

  useEffect(() => {
    let live = true;
    previewCampaignMessage(JSON.parse(messageKey))
      .then((result) => live && setPreview(result))
      .catch(() => live && setPreview(null));
    return () => {
      live = false;
    };
  }, [messageKey]);

  useEffect(() => {
    let live = true;
    previewCampaignAudience(JSON.parse(audienceKey))
      .then((result) => {
        if (!live) return;
        setAudience(result);
        setAudienceError(null);
      })
      .catch((error) => {
        if (!live) return;
        setAudience(null);
        setAudienceError(
          getErrorMessage(error, "Could not count the audience"),
        );
      });
    return () => {
      live = false;
    };
  }, [audienceKey]);

  const save = async () => {
    const parsed = campaignSchema.safeParse(form);
    if (!parsed.success) {
      setErrors(fieldErrors(parsed.error));
      return;
    }
    setErrors({});
    setBusy(true);
    try {
      const saved = campaign
        ? await updateCampaign(campaign.id, request)
        : await createCampaign(request);
      appToast.success(campaign ? "Saved" : "Saved as a draft");
      router.push(`/admin/notifications/${saved.id}`);
      router.refresh();
    } catch (error) {
      setErrors({ form: getErrorMessage(error, "Could not save it") });
    } finally {
      setBusy(false);
    }
  };

  const action = (index: number, key: "label" | "path", value: string) =>
    set(
      "actions",
      form.actions.map((a, i) => (i === index ? { ...a, [key]: value } : a)),
    );

  return (
    <form
      className="grid gap-8 lg:grid-cols-[minmax(0,1fr)_22rem]"
      noValidate
      onSubmit={(e) => {
        e.preventDefault();
        void save();
      }}
      data-testid="campaign-editor"
    >
      <div className="min-w-0 space-y-8">
        <section aria-labelledby="campaign-basics" className="space-y-4">
          <h2 id="campaign-basics" className="text-lg font-semibold">
            Campaign
          </h2>
          <div>
            <label className="label" htmlFor="campaign-name">
              Name
            </label>
            <input
              id="campaign-name"
              className={`input ${errors.name ? "input-error" : ""}`}
              value={form.name}
              maxLength={CAMPAIGN_LIMITS.name}
              onChange={(e) => set("name", e.target.value)}
              aria-invalid={Boolean(errors.name)}
            />
            {errors.name && <p className={errorText}>{errors.name}</p>}
          </div>
          <fieldset>
            <legend className="label">Purpose</legend>
            <div className="grid gap-1 sm:grid-cols-2">
              <label className="flex min-h-10 items-start gap-2 text-sm">
                <input
                  type="radio"
                  name="purpose"
                  className="mt-1"
                  checked={form.purpose === "lifecycle"}
                  onChange={() => set("purpose", "lifecycle")}
                />
                <span>
                  Lifecycle
                  <span className="block text-muted-foreground">
                    A product message, such as a welcome. Follows quiet hours.
                  </span>
                </span>
              </label>
              <label className="flex min-h-10 items-start gap-2 text-sm">
                <input
                  type="radio"
                  name="purpose"
                  className="mt-1"
                  checked={form.purpose === "marketing"}
                  onChange={() => set("purpose", "marketing")}
                />
                <span>
                  News and offers
                  <span className="block text-muted-foreground">
                    Only people who turned them on. Carries a one-tap opt-out.
                  </span>
                </span>
              </label>
            </div>
          </fieldset>
        </section>

        <section aria-labelledby="campaign-message" className="space-y-4">
          <h2 id="campaign-message" className="text-lg font-semibold">
            Message
          </h2>
          <p className="text-sm text-muted-foreground">
            <code>{"{firstName}"}</code> and <code>{"{appLink}"}</code> are
            filled in for each person.
          </p>
          <div>
            <label className="label" htmlFor="campaign-title">
              Title
            </label>
            <input
              id="campaign-title"
              className={`input ${errors.title ? "input-error" : ""}`}
              value={form.title}
              maxLength={CAMPAIGN_LIMITS.title}
              onChange={(e) => set("title", e.target.value)}
              aria-invalid={Boolean(errors.title)}
            />
            {errors.title && <p className={errorText}>{errors.title}</p>}
          </div>
          <div>
            <label className="label" htmlFor="campaign-body">
              Body
            </label>
            <textarea
              id="campaign-body"
              rows={4}
              className={`input ${errors.body ? "input-error" : ""}`}
              value={form.body}
              maxLength={CAMPAIGN_LIMITS.body}
              onChange={(e) => set("body", e.target.value)}
            />
            {errors.body && <p className={errorText}>{errors.body}</p>}
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <div>
              <label className="label" htmlFor="campaign-link">
                Opens
              </label>
              <input
                id="campaign-link"
                className="input"
                placeholder="/today"
                value={form.linkPath}
                onChange={(e) => set("linkPath", e.target.value)}
              />
            </div>
            <div>
              <label className="label" htmlFor="campaign-image">
                Image address
              </label>
              <input
                id="campaign-image"
                className="input"
                placeholder="https://"
                value={form.imageUrl}
                onChange={(e) => set("imageUrl", e.target.value)}
              />
            </div>
          </div>
          <fieldset className="space-y-2">
            <legend className="label">Buttons</legend>
            {form.actions.map((a, index) => (
              <div
                key={index}
                className="grid gap-2 sm:grid-cols-[1fr_1fr_auto]"
              >
                <input
                  className="input"
                  aria-label={`Button ${index + 1} label`}
                  placeholder="Label"
                  maxLength={40}
                  value={a.label}
                  onChange={(e) => action(index, "label", e.target.value)}
                />
                <input
                  className="input"
                  aria-label={`Button ${index + 1} page`}
                  placeholder="/meals"
                  value={a.path}
                  onChange={(e) => action(index, "path", e.target.value)}
                />
                <button
                  type="button"
                  className="btn-ghost-destructive btn-sm"
                  onClick={() =>
                    set(
                      "actions",
                      form.actions.filter((_, i) => i !== index),
                    )
                  }
                >
                  Remove
                </button>
              </div>
            ))}
            {form.actions.length < 2 && (
              <button
                type="button"
                className="btn-secondary btn-sm"
                onClick={() =>
                  set("actions", [...form.actions, { label: "", path: "" }])
                }
              >
                Add a button
              </button>
            )}
          </fieldset>
          <fieldset>
            <legend className="label">Channels</legend>
            <div className="grid grid-cols-2 gap-1 sm:grid-cols-4">
              {CHANNELS.map((c) => (
                <label
                  key={c.value}
                  className="flex min-h-10 items-center gap-2 text-sm"
                >
                  <input
                    type="checkbox"
                    checked={form.channels.includes(c.value)}
                    onChange={() => toggle("channels", c.value)}
                  />
                  {c.label}
                </label>
              ))}
            </div>
            {errors.channels && <p className={errorText}>{errors.channels}</p>}
          </fieldset>
        </section>

        <section aria-labelledby="campaign-timing" className="space-y-4">
          <h2 id="campaign-timing" className="text-lg font-semibold">
            When
          </h2>
          <div className="grid gap-4 sm:grid-cols-2">
            <div>
              <label className="label" id="campaign-send-label">
                Sends
              </label>
              <Select
                aria-labelledby="campaign-send-label"
                options={SENDS}
                value={form.send}
                onChange={(v) => set("send", v)}
              />
            </div>
            {form.send !== "now" && form.send !== "event" && (
              <div>
                <label className="label" id="campaign-zone-label">
                  Clock
                </label>
                <Select
                  aria-labelledby="campaign-zone-label"
                  options={zones}
                  value={form.timeZoneId}
                  onChange={(v) => set("timeZoneId", v)}
                  searchable
                  searchLabel="Find a time zone"
                />
              </div>
            )}
          </div>
          {(form.send === "once" || form.send === "every") && (
            <div className="grid gap-4 sm:grid-cols-2">
              <div>
                <label className="label" htmlFor="campaign-date">
                  {form.send === "once" ? "Date" : "Starting"}
                </label>
                <input
                  id="campaign-date"
                  type="date"
                  className="input"
                  value={form.date}
                  onChange={(e) => set("date", e.target.value)}
                />
              </div>
              <div>
                <label className="label" htmlFor="campaign-time">
                  Time
                </label>
                <input
                  id="campaign-time"
                  type="time"
                  className="input"
                  value={form.time}
                  onChange={(e) => set("time", e.target.value)}
                />
              </div>
            </div>
          )}
          {(form.send === "daily" || form.send === "days") && (
            <div className="max-w-48">
              <label className="label" htmlFor="campaign-time">
                Time
              </label>
              <input
                id="campaign-time"
                type="time"
                className="input"
                value={form.time}
                onChange={(e) => set("time", e.target.value)}
              />
            </div>
          )}
          {form.send === "days" && (
            <fieldset>
              <legend className="label">Days</legend>
              <div className="flex flex-wrap gap-x-4 gap-y-1">
                {DAYS.map((d) => (
                  <label
                    key={d}
                    className="flex min-h-10 items-center gap-2 text-sm capitalize"
                  >
                    <input
                      type="checkbox"
                      checked={form.days.includes(d)}
                      onChange={() => toggle("days", d)}
                    />
                    {d}
                  </label>
                ))}
              </div>
            </fieldset>
          )}
          {form.send === "every" && (
            <div className="grid gap-4 sm:grid-cols-2">
              <div>
                <label className="label" htmlFor="campaign-every-days">
                  Every how many days
                </label>
                <input
                  id="campaign-every-days"
                  inputMode="numeric"
                  className="input"
                  value={form.everyDays}
                  onChange={(e) => set("everyDays", e.target.value)}
                />
                {errors.everyDays && (
                  <p className={errorText}>{errors.everyDays}</p>
                )}
              </div>
              <div>
                <label className="label" htmlFor="campaign-every-hours">
                  Or every how many hours
                </label>
                <input
                  id="campaign-every-hours"
                  inputMode="numeric"
                  className="input"
                  value={form.everyHours}
                  onChange={(e) => set("everyHours", e.target.value)}
                />
                {errors.everyHours && (
                  <p className={errorText}>{errors.everyHours}</p>
                )}
              </div>
            </div>
          )}
          {form.send === "event" && (
            <div className="grid gap-4 sm:grid-cols-2">
              <div>
                <label className="label" id="campaign-event-label">
                  Event
                </label>
                <Select
                  aria-labelledby="campaign-event-label"
                  options={EVENTS}
                  value={form.event}
                  onChange={(v) => set("event", v)}
                />
              </div>
              <div>
                <label className="label" htmlFor="campaign-event-days">
                  Days
                </label>
                <input
                  id="campaign-event-days"
                  inputMode="numeric"
                  className="input"
                  value={form.eventDays}
                  onChange={(e) => set("eventDays", e.target.value)}
                />
                {errors.eventDays && (
                  <p className={errorText}>{errors.eventDays}</p>
                )}
              </div>
            </div>
          )}
        </section>

        <section aria-labelledby="campaign-audience" className="space-y-4">
          <h2 id="campaign-audience" className="text-lg font-semibold">
            Who
          </h2>
          <p className="text-sm text-muted-foreground">
            Every filter left empty matches everyone.
          </p>
          <fieldset>
            <legend className="label">Plans</legend>
            <div className="flex flex-wrap gap-x-4 gap-y-1">
              {PLANS.map((p) => (
                <label
                  key={p.value}
                  className="flex min-h-10 items-center gap-2 text-sm"
                >
                  <input
                    type="checkbox"
                    checked={form.plans.includes(p.value)}
                    onChange={() => toggle("plans", p.value)}
                  />
                  {p.label}
                </label>
              ))}
            </div>
          </fieldset>
          <div className="grid gap-4 sm:grid-cols-2">
            <div>
              <label className="label" htmlFor="campaign-from">
                Signed up from
              </label>
              <input
                id="campaign-from"
                type="date"
                className="input"
                value={form.signedUpFrom}
                onChange={(e) => set("signedUpFrom", e.target.value)}
              />
            </div>
            <div>
              <label className="label" htmlFor="campaign-to">
                Signed up until
              </label>
              <input
                id="campaign-to"
                type="date"
                className="input"
                value={form.signedUpTo}
                onChange={(e) => set("signedUpTo", e.target.value)}
              />
            </div>
            <div>
              <label className="label" htmlFor="campaign-inactive">
                Inactive for at least (days)
              </label>
              <input
                id="campaign-inactive"
                inputMode="numeric"
                className="input"
                value={form.inactiveDays}
                onChange={(e) => set("inactiveDays", e.target.value)}
              />
              {errors.inactiveDays && (
                <p className={errorText}>{errors.inactiveDays}</p>
              )}
            </div>
            <div>
              <label className="label" htmlFor="campaign-zones">
                Time zones
              </label>
              <input
                id="campaign-zones"
                className="input"
                placeholder="Africa/Addis_Ababa, Europe/London"
                value={form.timeZones}
                onChange={(e) => set("timeZones", e.target.value)}
              />
            </div>
            <div>
              <label className="label" id="campaign-telegram-label">
                Linked to Telegram
              </label>
              <Select
                aria-labelledby="campaign-telegram-label"
                options={TRISTATE}
                value={form.telegramLinked}
                onChange={(v) => set("telegramLinked", v as Tristate)}
              />
            </div>
            <div>
              <label className="label" id="campaign-household-label">
                In a household
              </label>
              <Select
                aria-labelledby="campaign-household-label"
                options={TRISTATE}
                value={form.hasHousehold}
                onChange={(v) => set("hasHousehold", v as Tristate)}
              />
            </div>
          </div>
        </section>

        {errors.form && (
          <p
            role="alert"
            className="text-sm text-burnt-peach-700 dark:text-burnt-peach-400"
          >
            {errors.form}
          </p>
        )}
        <div className="flex flex-wrap justify-end gap-2">
          <button type="submit" className="btn-primary" disabled={busy}>
            {busy ? "Saving…" : campaign ? "Save changes" : "Save draft"}
          </button>
        </div>
      </div>

      <aside
        className="min-w-0 space-y-6 lg:sticky lg:top-20 lg:self-start"
        aria-label="Preview"
      >
        <section aria-labelledby="campaign-reach" className="space-y-2">
          <h2
            id="campaign-reach"
            className="text-sm font-semibold uppercase tracking-wide text-muted-foreground"
          >
            Audience
          </h2>
          {audience ? (
            <div role="status" className="space-y-1 text-sm">
              <p>
                <strong
                  className="tabular-nums"
                  data-testid="campaign-reachable"
                >
                  {audience.reachable}
                </strong>{" "}
                {audience.reachable === 1 ? "person" : "people"} reachable of{" "}
                <span className="tabular-nums">{audience.matched}</span> matched
              </p>
              <p className="text-muted-foreground">{audience.label}</p>
              <ul className="text-muted-foreground">
                {audience.channels.map((c) => (
                  <li key={c.channel} className="flex justify-between gap-4">
                    <span>
                      {CHANNELS.find((x) => x.value === c.channel)?.label ??
                        c.channel}
                    </span>
                    <span className="tabular-nums">{c.reachable}</span>
                  </li>
                ))}
              </ul>
            </div>
          ) : (
            <p className="text-sm text-muted-foreground">
              {audienceError ?? "Counting…"}
            </p>
          )}
        </section>
        {preview && <ChannelPreviews preview={preview} />}
      </aside>
    </form>
  );
}
