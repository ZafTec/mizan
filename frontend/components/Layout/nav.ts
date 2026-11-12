import type { IconName } from "@/components/ui/icon";

export type NavItem = {
  href: string;
  label: string;
  icon: IconName;
  description?: string;
  adminOnly?: boolean;
};

export type NavGroup = {
  label: string;
  items: NavItem[];
};

/**
 * NAVIGATION TIERS - see docs/ARCHITECTURE.md#navigation-and-logging.
 *
 * Tier 1 (SPINE) is the only permanent navigation. Four destinations and one
 * action, on every route, at both breakpoints.
 *
 * Tier 2 is contextual and lives in the pages themselves.
 * Tier 3 is MORE_GROUPS, rendered by /more - two taps, zero permanent pixels.
 */

/**
 * The spine is a daily log, a dated history, and trends across all three logs.
 */
export const SPINE: NavItem[] = [
  { href: "/today", label: "Today", icon: "home" },
  { href: "/history", label: "History", icon: "calendarCheck" },
  { href: "/progress", label: "Progress", icon: "chartLine" },
  { href: "/more", label: "More", icon: "menu" },
];

/** The ( + ) action. The three things this app exists to record. */
export const LOG_ACTIONS: NavItem[] = [
  {
    href: "/meals/add",
    label: "Meal",
    icon: "flame",
    description: "Log food against today's targets",
  },
  {
    href: "/workouts",
    label: "Workout",
    icon: "activity",
    description: "Start or resume a training session",
  },
  {
    href: "/body-measurements",
    label: "Measurement",
    icon: "chartLine",
    description: "Weight and body measurements",
  },
];

/** Which waiting count from the admin overview a section shows beside its name. */
export type AdminAttentionKey =
  | "deadJobs"
  | "failedEmails"
  | "newContactMessages"
  | "openPriceReports"
  | "openContentReports";

export type AdminSection = {
  href: string;
  label: string;
  attention?: AdminAttentionKey;
};

/**
 * Every section under /admin, grouped by the job it serves. The admin sidebar
 * (and its sheet on a phone) renders these; /more links to /admin once.
 */
export const ADMIN_GROUPS: { label: string; items: AdminSection[] }[] = [
  { label: "Today", items: [{ href: "/admin", label: "Overview" }] },
  {
    label: "People",
    items: [
      { href: "/admin/users", label: "Users" },
      { href: "/admin/households", label: "Households" },
      { href: "/admin/relationships", label: "Coaching links" },
      { href: "/admin/sessions", label: "Sessions" },
    ],
  },
  {
    label: "Catalogue",
    items: [
      { href: "/admin/ingredients", label: "Foods" },
      { href: "/admin/recipes", label: "Recipes" },
      { href: "/admin/exercises", label: "Exercises" },
      { href: "/admin/achievements", label: "Achievements" },
      { href: "/admin/meal-plans", label: "Meal plans" },
      { href: "/admin/prices", label: "Prices", attention: "openPriceReports" },
    ],
  },
  {
    label: "Inbox",
    items: [
      {
        href: "/admin/contact",
        label: "Contact",
        attention: "newContactMessages",
      },
      {
        href: "/admin/moderation",
        label: "Moderation",
        attention: "openContentReports",
      },
      { href: "/admin/mail", label: "Mail", attention: "failedEmails" },
    ],
  },
  {
    label: "Outreach",
    items: [{ href: "/admin/notifications", label: "Campaigns" }],
  },
  {
    label: "Plans and AI",
    items: [
      { href: "/admin/billing", label: "Billing" },
      { href: "/admin/usage", label: "Usage" },
      { href: "/admin/usage-limits", label: "Usage limits" },
      { href: "/admin/ai", label: "Assistant" },
    ],
  },
  {
    label: "System",
    items: [
      { href: "/admin/settings", label: "Settings" },
      { href: "/admin/feature-flags", label: "Feature flags" },
      { href: "/admin/jobs", label: "Jobs", attention: "deadJobs" },
      { href: "/admin/audit-logs", label: "Audit log" },
    ],
  },
];

/** The section a path belongs to: the longest href that prefixes it. */
export function activeAdminHref(pathname: string): string | undefined {
  return ADMIN_GROUPS.flatMap((g) => g.items)
    .map((i) => i.href)
    .filter((href) =>
      href === "/admin"
        ? pathname === "/admin"
        : pathname === href || pathname.startsWith(`${href}/`),
    )
    .sort((a, b) => b.length - a.length)[0];
}

/**
 * Tier 3. Everything the product does that is not the spine.
 *
 * This list is also the fix for the six real orphans found in phase 1:
 * /trainers/my-trainer, /trainers/requests, /admin/moderation and
 * /admin/recipes had no inbound link anywhere in the app. /admin/relationships
 * was deleted in phase 3 because it always returned [] - it has a real backend
 * query now (phase 13) and is back.
 */
export const MORE_GROUPS: NavGroup[] = [
  {
    label: "Food",
    items: [
      {
        href: "/recipes",
        label: "Recipes",
        icon: "cookingPot",
        description: "Saved meals you log in one tap",
      },
      {
        href: "/meal-plan",
        label: "Meal Plan",
        icon: "calendarCheck",
        description: "Plan the week with your household",
      },
      {
        href: "/meal-plan/shopping-list",
        label: "Shopping List",
        icon: "cart",
        description: "What the plan needs you to buy",
      },
      {
        href: "/ingredients",
        label: "Foods",
        icon: "search",
        description: "The ingredient database",
      },
    ],
  },
  {
    label: "Fitness",
    items: [
      {
        href: "/workouts",
        label: "Workouts",
        icon: "activity",
        description: "Sessions, templates and history",
      },
      {
        href: "/exercises",
        label: "Exercises",
        icon: "zap",
        description: "Movement library",
      },
      {
        href: "/body-measurements",
        label: "Body",
        icon: "chartLine",
        description: "Measurement history",
      },
      {
        href: "/goal",
        label: "Goals",
        icon: "rocket",
        description: "Targets and progress",
      },
      {
        href: "/achievements",
        label: "Achievements",
        icon: "sparkles",
        description: "Streak, points and badges",
      },
    ],
  },
  {
    label: "People",
    items: [
      {
        href: "/social",
        label: "Feed",
        icon: "users",
        description: "Training shared by people you approve",
      },
      {
        href: "/trainers",
        label: "Find a Trainer",
        icon: "heart",
        description: "Browse available trainers",
      },
      {
        href: "/trainers/my-trainer",
        label: "My Trainer",
        icon: "heart",
        description: "Your current coaching relationship",
      },
      {
        href: "/trainers/requests",
        label: "Trainer Requests",
        icon: "bell",
        description: "Pending requests to and from you",
      },
      {
        href: "/messaging",
        label: "Messages",
        icon: "messageCircle",
        description: "Chat with your coach",
      },
      {
        href: "/profile/household",
        label: "Household",
        icon: "home",
        description: "Share plans and lists",
      },
    ],
  },
  {
    // Assistant and integration entries share the same log and backend.
    // See docs/AI.md and docs/MCP.md.
    label: "Assistant",
    items: [
      {
        href: "/ai",
        label: "AI Coach",
        icon: "brain",
        description: "Ask about your log",
      },
      {
        href: "/onboarding",
        label: "Guided setup",
        icon: "sparkles",
        description: "Set your targets by conversation",
      },
      {
        href: "/profile/settings/telegram",
        label: "Telegram",
        icon: "messageCircle",
        description: "Log from a photo without opening the app",
      },
      {
        href: "/profile/mcp",
        label: "Connected apps",
        icon: "bot",
        description: "Choose what AI assistants can do",
      },
    ],
  },
  {
    label: "Account",
    items: [
      {
        href: "/profile",
        label: "Profile",
        icon: "user",
        description: "Your account",
      },
      {
        href: "/notifications",
        label: "Notifications",
        icon: "bell",
        description: "What happened while you were away",
      },
      {
        href: "/billing",
        label: "Billing",
        icon: "sparkles",
        description: "Your plan and invoices",
      },
      {
        href: "/profile/settings#assistant",
        label: "Usage",
        icon: "chartLine",
        description: "Assistant permissions and usage",
      },
      {
        href: "/profile/settings#export",
        label: "Export",
        icon: "upload",
        description: "Download your log and account data",
      },
      {
        href: "/profile/sessions",
        label: "Sessions",
        icon: "lock",
        description: "Where you are signed in",
      },
      {
        href: "/profile/settings",
        label: "Settings",
        icon: "settings",
        description: "Name, time zone, appearance, assistant",
      },
    ],
  },
  {
    label: "Admin",
    items: [
      {
        href: "/admin",
        label: "Administration",
        icon: "shieldCheck",
        description: "People, catalogue, inbox and system settings",
        adminOnly: true,
      },
    ],
  },
];

/** Avatar dropdown: the handful of account links worth one click from anywhere. */
export const USER_MENU: NavItem[] = [
  { href: "/profile", label: "Profile", icon: "user" },
  { href: "/billing", label: "Billing", icon: "sparkles" },
  { href: "/profile/settings", label: "Settings", icon: "settings" },
  { href: "/admin", label: "Admin", icon: "shieldCheck", adminOnly: true },
];

export function visibleGroups(isAdmin: boolean): NavGroup[] {
  return MORE_GROUPS.map((g) => ({
    ...g,
    items: g.items.filter((i) => !i.adminOnly || isAdmin),
  })).filter((g) => g.items.length > 0);
}

export function isActive(pathname: string | null, href: string): boolean {
  if (!pathname) return false;
  if (href === "/today" || href === "/more") return pathname === href;
  return pathname === href || pathname.startsWith(`${href}/`);
}
