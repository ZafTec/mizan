import { z } from "zod";

/**
 * The contact, announcement, reply and note forms as a person types them,
 * checked before the round trip. The API checks again with the same limits
 * (ContactSubmission, SendAnnouncementCommandValidator, SupportNote).
 */
export const CONTACT_LIMITS = {
  name: 120,
  email: 320,
  subject: 160,
  message: 5000,
} as const;

const text = (label: string, max: number) =>
  z
    .string()
    .trim()
    .min(1, `${label} is required`)
    .max(
      max,
      `Keep the ${label.toLowerCase()} under ${max.toLocaleString("en")} characters`,
    );

export const contactSchema = z.object({
  name: text("Name", CONTACT_LIMITS.name),
  email: z
    .string()
    .trim()
    .min(1, "Email is required")
    .max(CONTACT_LIMITS.email, "That address is too long")
    .email("Enter an email address we can reply to"),
  subject: text("Subject", CONTACT_LIMITS.subject),
  message: text("Message", CONTACT_LIMITS.message),
});
export type ContactForm = z.infer<typeof contactSchema>;

export const AUDIENCE_KINDS = [
  "all",
  "pro",
  "free",
  "household",
  "user",
] as const;
export type AudienceKind = (typeof AUDIENCE_KINDS)[number];

export const announcementSchema = z
  .object({
    subject: text("Subject", 200),
    body: text("Message", 20_000),
    kind: z.enum(AUDIENCE_KINDS),
    targetId: z.string().nullable(),
  })
  .refine(
    (value) =>
      (value.kind !== "household" && value.kind !== "user") ||
      Boolean(value.targetId),
    {
      path: ["targetId"],
      message: "Choose who receives it",
    },
  );
export type AnnouncementForm = z.infer<typeof announcementSchema>;

/** The audience as the API spells it: all, pro, free, household:{id} or user:{id}. */
export function audienceOf(form: Pick<AnnouncementForm, "kind" | "targetId">) {
  return form.kind === "household" || form.kind === "user"
    ? `${form.kind}:${form.targetId}`
    : form.kind;
}

export const replySchema = z.object({ body: text("Reply", 5000) });
export const supportNoteSchema = z.object({ body: text("Note", 4000) });

/** The first message per field, for showing under each input. */
export function fieldErrors(error: z.ZodError): Record<string, string> {
  const out: Record<string, string> = {};
  for (const issue of error.issues) {
    const key = String(issue.path[0] ?? "form");
    out[key] ??= issue.message;
  }
  return out;
}
