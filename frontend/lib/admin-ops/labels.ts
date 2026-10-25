/**
 * Words and links for what the API returns about mail and health. Display
 * only: statuses and kinds arrive from the API and are shown as they are when
 * a label is missing.
 */
export const MAIL_KIND_LABELS: Record<string, string> = {
  "auth.verify": "Email confirmation",
  "auth.reset": "Password reset",
  "auth.signin": "Sign-in alert",
  "admin.announcement": "Announcement",
  "contact.received": "Contact notice",
  "contact.reply": "Contact reply",
  "settings.test": "Test email",
};

export const mailKindLabel = (kind: string) => MAIL_KIND_LABELS[kind] ?? kind;

export type HealthTone = "good" | "warn" | "bad" | "neutral";

export const HEALTH_STATUS: Record<
  string,
  { label: string; tone: HealthTone }
> = {
  ok: { label: "OK", tone: "good" },
  degraded: { label: "Degraded", tone: "warn" },
  down: { label: "Down", tone: "bad" },
  notConfigured: { label: "Not configured", tone: "neutral" },
};

/** Each check's name, and the admin page where its cause is fixed. */
export const HEALTH_CHECKS: Record<
  string,
  { label: string; href?: string; linkText?: string }
> = {
  database: { label: "Database" },
  redis: { label: "Redis", href: "/admin/settings", linkText: "Settings" },
  outbox: { label: "Background jobs", href: "/admin/jobs", linkText: "Jobs" },
  email: { label: "Email", href: "/admin/mail", linkText: "Mail log" },
  ai: { label: "AI provider", href: "/admin/ai", linkText: "Assistant" },
  telegram: {
    label: "Telegram bot",
    href: "/admin/settings",
    linkText: "Settings",
  },
  push: {
    label: "Push notifications",
    href: "/admin/settings",
    linkText: "Settings",
  },
  prices: { label: "Price feed", href: "/admin/prices", linkText: "Prices" },
  storage: {
    label: "File storage",
    href: "/admin/settings",
    linkText: "Settings",
  },
};
