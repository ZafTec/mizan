import { z } from "zod";
import type {
  NotificationCampaign,
  SaveCampaignRequest,
} from "@/lib/api/admin-notifications";

/**
 * The campaign editor as the administrator types it, checked before the round
 * trip. The API checks again with the same limits (CampaignRules) and owns
 * every other rule: audiences, timing and placeholders.
 */
export const CAMPAIGN_LIMITS = { name: 120, title: 150, body: 500 } as const;

export const CHANNELS = [
  { value: "inApp", label: "In the app" },
  { value: "telegram", label: "Telegram" },
  { value: "push", label: "Push" },
  { value: "email", label: "Email" },
] as const;

export const PLANS = [
  { value: "free", label: "Free" },
  { value: "pro", label: "Pro" },
  { value: "lifetime", label: "Lifetime" },
] as const;

export const SENDS = [
  { value: "now", label: "As soon as it is started" },
  { value: "once", label: "Once, at a date and time" },
  { value: "daily", label: "Every day" },
  { value: "days", label: "On chosen weekdays" },
  { value: "every", label: "Every few days or hours" },
  { value: "event", label: "After an event" },
] as const;

export const EVENTS = [
  { value: "user.signed_up", label: "Days after sign-up" },
  { value: "user.inactive", label: "Days without activity" },
  { value: "subscription.pro_ends", label: "Days before Pro ends" },
] as const;

export const DAYS = ["mon", "tue", "wed", "thu", "fri", "sat", "sun"] as const;

export const TRISTATE = [
  { value: "any", label: "Either" },
  { value: "yes", label: "Yes" },
  { value: "no", label: "No" },
] as const;
export type Tristate = (typeof TRISTATE)[number]["value"];

export type CampaignForm = {
  name: string;
  purpose: "marketing" | "lifecycle";
  title: string;
  body: string;
  imageUrl: string;
  linkPath: string;
  actions: { label: string; path: string }[];
  channels: string[];
  send: string;
  date: string;
  time: string;
  days: string[];
  everyDays: string;
  everyHours: string;
  event: string;
  eventDays: string;
  timeZoneId: string;
  plans: string[];
  signedUpFrom: string;
  signedUpTo: string;
  inactiveDays: string;
  telegramLinked: Tristate;
  hasHousehold: Tristate;
  timeZones: string;
};

export const EMPTY_CAMPAIGN: CampaignForm = {
  name: "",
  purpose: "lifecycle",
  title: "",
  body: "",
  imageUrl: "",
  linkPath: "",
  actions: [],
  channels: ["inApp"],
  send: "now",
  date: "",
  time: "09:00",
  days: [],
  everyDays: "",
  everyHours: "",
  event: "user.signed_up",
  eventDays: "1",
  timeZoneId: "UTC",
  plans: [],
  signedUpFrom: "",
  signedUpTo: "",
  inactiveDays: "",
  telegramLinked: "any",
  hasHousehold: "any",
  timeZones: "",
};

const optionalWhole = z
  .string()
  .trim()
  .refine((v) => v === "" || /^\d+$/.test(v), "Use a whole number");

export const campaignSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, "Give the campaign a name")
    .max(CAMPAIGN_LIMITS.name, `At most ${CAMPAIGN_LIMITS.name} characters`),
  title: z
    .string()
    .trim()
    .min(1, "Give the message a title")
    .max(CAMPAIGN_LIMITS.title, `At most ${CAMPAIGN_LIMITS.title} characters`),
  body: z
    .string()
    .max(CAMPAIGN_LIMITS.body, `At most ${CAMPAIGN_LIMITS.body} characters`),
  channels: z.array(z.string()).min(1, "Choose at least one channel"),
  everyDays: optionalWhole,
  everyHours: optionalWhole,
  eventDays: optionalWhole,
  inactiveDays: optionalWhole,
});

const blank = (value: string) => (value.trim() === "" ? null : value.trim());
const whole = (value: string) =>
  value.trim() === "" ? null : Number.parseInt(value, 10);
const tristate = (value: Tristate) =>
  value === "any" ? null : value === "yes";

/** The form as the API's request: text as typed, empty fields left out. */
export function requestOf(form: CampaignForm): SaveCampaignRequest {
  return {
    name: form.name.trim(),
    purpose: form.purpose,
    message: {
      title: form.title.trim(),
      body: blank(form.body),
      imageUrl: blank(form.imageUrl),
      linkPath: blank(form.linkPath),
      actions: form.actions.filter((a) => a.label.trim() || a.path.trim()),
    },
    timing: {
      send: form.send,
      date: blank(form.date),
      time: blank(form.time),
      days: form.send === "days" ? form.days : null,
      everyDays: form.send === "every" ? whole(form.everyDays) : null,
      everyHours: form.send === "every" ? whole(form.everyHours) : null,
      event: form.send === "event" ? form.event : null,
      eventDays: form.send === "event" ? whole(form.eventDays) : null,
    },
    audience: {
      plans: form.plans,
      signedUpFrom: blank(form.signedUpFrom),
      signedUpTo: blank(form.signedUpTo),
      inactiveDays: whole(form.inactiveDays),
      telegramLinked: tristate(form.telegramLinked),
      hasHousehold: tristate(form.hasHousehold),
      timeZones: form.timeZones
        .split(",")
        .map((z) => z.trim())
        .filter(Boolean),
    },
    channels: form.channels,
    timeZoneId: blank(form.timeZoneId),
  };
}

const text = (value: number | null | undefined) =>
  value === null || value === undefined ? "" : String(value);
const fromBool = (value: boolean | null | undefined): Tristate =>
  value === null || value === undefined ? "any" : value ? "yes" : "no";

/** A saved campaign back in the editor. */
export function formOf(campaign: NotificationCampaign): CampaignForm {
  const { message, timing, audience } = campaign;
  return {
    name: campaign.name,
    purpose: campaign.purpose === "marketing" ? "marketing" : "lifecycle",
    title: message.title,
    body: message.body ?? "",
    imageUrl: message.imageUrl ?? "",
    linkPath: message.linkPath ?? "",
    actions: (message.actions ?? []).map((a) => ({
      label: a.label,
      path: a.path,
    })),
    channels: [...campaign.channels],
    send: timing.send,
    date: timing.date ?? "",
    time: timing.time ?? "",
    days: [...(timing.days ?? [])],
    everyDays: text(timing.everyDays),
    everyHours: text(timing.everyHours),
    event: timing.event ?? "user.signed_up",
    eventDays: text(timing.eventDays),
    timeZoneId: campaign.timeZoneId,
    plans: [...(audience.plans ?? [])],
    signedUpFrom: audience.signedUpFrom ?? "",
    signedUpTo: audience.signedUpTo ?? "",
    inactiveDays: text(audience.inactiveDays),
    telegramLinked: fromBool(audience.telegramLinked),
    hasHousehold: fromBool(audience.hasHousehold),
    timeZones: (audience.timeZones ?? []).join(", "),
  };
}
