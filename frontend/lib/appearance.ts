export type ThemePreference = "light" | "dark" | "system";
export type EffectiveTheme = "light" | "dark";

export interface AppearanceSettings {
	theme: ThemePreference;
	compactMode: boolean;
	reduceAnimations: boolean;
}

export interface AppearanceUserFields {
	themePreference?: string | null;
	compactMode?: boolean | null;
	reduceAnimations?: boolean | null;
}

export const defaultAppearanceSettings: AppearanceSettings = {
	theme: "system",
	compactMode: false,
	reduceAnimations: false,
};

function normalizeThemePreference(value?: unknown): ThemePreference {
	return value === "light" || value === "dark" || value === "system"
		? value
		: defaultAppearanceSettings.theme;
}

export function normalizeAppearanceSettings(
	settings?: Partial<AppearanceSettings> | null
): AppearanceSettings {
	return {
		theme: normalizeThemePreference(settings?.theme),
		compactMode: Boolean(settings?.compactMode),
		reduceAnimations: Boolean(settings?.reduceAnimations),
	};
}

export function getAppearanceSettingsFromUser(
	user?: AppearanceUserFields | null
): AppearanceSettings {
	if (!user) return defaultAppearanceSettings;
	return normalizeAppearanceSettings({
		theme: normalizeThemePreference(user.themePreference),
		compactMode: Boolean(user.compactMode),
		reduceAnimations: Boolean(user.reduceAnimations),
	});
}

export function resolveEffectiveTheme(pref: ThemePreference): EffectiveTheme {
	if (pref === "system") {
		if (typeof window === "undefined") return "light"; // SSR default; pre-hydration script fixes it
		return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
	}
	return pref;
}

export function applyAppearanceClasses(settings: AppearanceSettings): void {
	if (typeof document === "undefined") return;
	const root = document.documentElement;
	const effective = resolveEffectiveTheme(settings.theme);
	root.classList.toggle("dark", effective === "dark");
	root.classList.toggle("compact", settings.compactMode);
	root.classList.toggle("reduce-motion", settings.reduceAnimations);
}

// Server-side: used from layout.tsx to pre-set classes based on cookie or user record.
// "system" intentionally produces no class on SSR — the inline pre-hydration script
// picks the correct class before React mounts using window.matchMedia.
export function getServerAppearanceClasses(settings: AppearanceSettings): string[] {
	const classes: string[] = [];
	if (settings.theme === "dark") classes.push("dark");
	if (settings.compactMode) classes.push("compact");
	if (settings.reduceAnimations) classes.push("reduce-motion");
	return classes;
}
