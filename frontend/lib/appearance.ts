export type ThemePreference = "light" | "dark" | "system";

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

export const APPEARANCE_STORAGE_KEY = "macrochef-appearance";

export const defaultAppearanceSettings: AppearanceSettings = {
	theme: "system",
	compactMode: false,
	reduceAnimations: false,
};

function normalizeThemePreference(value?: string | null): ThemePreference {
	return value === "light" || value === "dark" || value === "system"
		? value
		: defaultAppearanceSettings.theme;
}

export function normalizeAppearanceSettings(
	settings?: Partial<AppearanceSettings> | null
): AppearanceSettings {
	return {
		theme: normalizeThemePreference(settings?.theme),
		compactMode: settings?.compactMode ?? defaultAppearanceSettings.compactMode,
		reduceAnimations:
			settings?.reduceAnimations ?? defaultAppearanceSettings.reduceAnimations,
	};
}

export function getAppearanceSettingsFromUser(
	user?: AppearanceUserFields | null
): AppearanceSettings {
	return normalizeAppearanceSettings({
		theme: normalizeThemePreference(user?.themePreference),
		compactMode: Boolean(user?.compactMode),
		reduceAnimations: Boolean(user?.reduceAnimations),
	});
}

export function getStoredAppearanceSettings(): AppearanceSettings {
	if (typeof window === "undefined") {
		return defaultAppearanceSettings;
	}

	try {
		const stored = localStorage.getItem(APPEARANCE_STORAGE_KEY);
		if (!stored) {
			return defaultAppearanceSettings;
		}

		return normalizeAppearanceSettings(JSON.parse(stored));
	} catch {
		return defaultAppearanceSettings;
	}
}

export function storeAppearanceSettings(settings: AppearanceSettings) {
	if (typeof window === "undefined") {
		return;
	}

	localStorage.setItem(APPEARANCE_STORAGE_KEY, JSON.stringify(settings));
}

function applyTheme(theme: ThemePreference) {
	if (typeof window === "undefined") {
		return;
	}

	const root = document.documentElement;
	if (theme === "system") {
		const prefersDark = window.matchMedia("(prefers-color-scheme: dark)").matches;
		root.classList.toggle("dark", prefersDark);
		return;
	}

	root.classList.toggle("dark", theme === "dark");
}

export function applyAppearanceSettings(settings: AppearanceSettings) {
	if (typeof window === "undefined") {
		return;
	}

	applyTheme(settings.theme);
	document.documentElement.classList.toggle("compact", settings.compactMode);
	document.documentElement.classList.toggle(
		"reduce-motion",
		settings.reduceAnimations
	);
	storeAppearanceSettings(settings);
}

export function getServerAppearanceClasses(settings: AppearanceSettings): string[] {
	const classes: string[] = [];

	if (settings.theme === "dark") {
		classes.push("dark");
	}

	if (settings.compactMode) {
		classes.push("compact");
	}

	if (settings.reduceAnimations) {
		classes.push("reduce-motion");
	}

	return classes;
}
