import type { AnimatedIconName } from "@/components/ui/animated-icon";

export type NavItem = {
	href: string;
	label: string;
	icon: AnimatedIconName;
	description?: string;
	adminOnly?: boolean;
};

export type NavGroup = {
	label: string;
	items: NavItem[];
};

/**
 * NAVIGATION TIERS - see docs/REFOCUS.md §3.
 *
 * Tier 1 (SPINE) is the only permanent navigation. Four destinations and one
 * action, on every route, at both breakpoints. Nothing else gets a permanent
 * slot: the audit in docs/ROUTE-AUDIT.md found 73 routes competing for 21 flat
 * nav entries, which is why nothing felt findable.
 *
 * Tier 2 is contextual and lives in the pages themselves (phase 5).
 * Tier 3 is MORE_GROUPS, rendered by /more - two taps, zero permanent pixels.
 */

/**
 * Spine destinations point at the routes that exist today. Phase 11 rebuilds
 * /today, /history and /progress and repoints these three hrefs; the shell
 * itself does not change when that happens.
 */
export const SPINE: NavItem[] = [
	{ href: "/dashboard", label: "Today", icon: "home" },
	{ href: "/meals", label: "History", icon: "flame" },
	{ href: "/goal/dashboard", label: "Progress", icon: "chartLine" },
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

/**
 * Tier 3. Everything the product does that is not the spine.
 *
 * This list is also the fix for five of the six real orphans found in phase 1:
 * /trainers/my-trainer, /trainers/requests, /admin/moderation and
 * /admin/recipes had no inbound link anywhere in the app. The sixth,
 * /admin/relationships, was deleted in phase 3 - it always returned [].
 */
export const MORE_GROUPS: NavGroup[] = [
	{
		label: "Food",
		items: [
			{ href: "/recipes", label: "Recipes", icon: "cookingPot", description: "Saved meals you log in one tap" },
			{ href: "/meal-plan", label: "Meal Plan", icon: "calendarCheck", description: "Plan the week with your household" },
			{ href: "/meal-plan/shopping-list", label: "Shopping List", icon: "cart", description: "What the plan needs you to buy" },
			{ href: "/ingredients", label: "Foods", icon: "search", description: "The ingredient database" },
		],
	},
	{
		label: "Fitness",
		items: [
			{ href: "/workouts", label: "Workouts", icon: "activity", description: "Sessions, templates and history" },
			{ href: "/exercises", label: "Exercises", icon: "zap", description: "Movement library" },
			{ href: "/body-measurements", label: "Body", icon: "chartLine", description: "Measurement history" },
			{ href: "/goal", label: "Goals", icon: "rocket", description: "Targets and progress" },
			{ href: "/achievements", label: "Achievements", icon: "sparkles" },
		],
	},
	{
		label: "People",
		items: [
			{ href: "/social", label: "Feed", icon: "users" },
			{ href: "/trainers", label: "Find a Trainer", icon: "heart", description: "Browse available trainers" },
			{ href: "/trainers/my-trainer", label: "My Trainer", icon: "heart", description: "Your current coaching relationship" },
			{ href: "/trainers/requests", label: "Trainer Requests", icon: "bell", description: "Pending requests to and from you" },
			{ href: "/messaging", label: "Messages", icon: "messageCircle" },
			{ href: "/profile/household", label: "Household", icon: "home", description: "Share plans and lists" },
		],
	},
	{
		// Both entries are agent interfaces over the same log. Phase 10 grows
		// this group: chat, food analysis, usage - see docs/REFOCUS.md §10.
		label: "Assistant",
		items: [
			{ href: "/ai", label: "AI Coach", icon: "brain", description: "Ask about your log" },
			{ href: "/profile/mcp", label: "MCP Tokens", icon: "bot", description: "Connect an agent to your log" },
		],
	},
	{
		label: "Account",
		items: [
			{ href: "/profile", label: "Profile", icon: "user" },
			{ href: "/notifications", label: "Notifications", icon: "bell" },
			{ href: "/billing", label: "Billing", icon: "sparkles" },
			{ href: "/profile/sessions", label: "Sessions", icon: "lock" },
			{ href: "/profile/settings", label: "Settings", icon: "settings" },
		],
	},
	{
		label: "Admin",
		items: [
			{ href: "/admin", label: "Overview", icon: "shieldCheck", adminOnly: true },
			{ href: "/admin/users", label: "Users", icon: "users", adminOnly: true },
			{ href: "/admin/ingredients", label: "Ingredients", icon: "search", adminOnly: true },
			{ href: "/admin/recipes", label: "Recipes", icon: "cookingPot", adminOnly: true },
			{ href: "/admin/exercises", label: "Exercises", icon: "zap", adminOnly: true },
			{ href: "/admin/moderation", label: "Moderation", icon: "badgeAlert", adminOnly: true },
			{ href: "/admin/ai", label: "Assistant", icon: "bot", adminOnly: true },
			{ href: "/admin/households", label: "Households", icon: "home", adminOnly: true },
			{ href: "/admin/sessions", label: "Sessions", icon: "lock", adminOnly: true },
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
	if (href === "/dashboard" || href === "/more") return pathname === href;
	return pathname === href || pathname.startsWith(`${href}/`);
}
