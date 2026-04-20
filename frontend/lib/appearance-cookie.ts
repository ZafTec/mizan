// SYNC: keys here must match the pre-hydration script in lib/appearance-script.ts
// and the server-side reader in app/layout.tsx.

import {
	defaultAppearanceSettings,
	normalizeAppearanceSettings,
	type AppearanceSettings,
} from "./appearance";

export const APPEARANCE_COOKIE = "mizan-appearance";
const COOKIE_MAX_AGE_SECONDS = 60 * 60 * 24 * 365; // 1 year

export function parseAppearanceCookie(
	raw?: string | null
): AppearanceSettings | null {
	if (!raw) return null;
	try {
		const parsed = JSON.parse(decodeURIComponent(raw));
		return normalizeAppearanceSettings(parsed);
	} catch {
		return null;
	}
}

export function readAppearanceCookie(): AppearanceSettings | null {
	if (typeof document === "undefined") return null;
	const match = document.cookie.match(
		new RegExp(`(?:^|;\\s*)${APPEARANCE_COOKIE}=([^;]*)`)
	);
	return match ? parseAppearanceCookie(match[1]) : null;
}

export function writeAppearanceCookie(settings: AppearanceSettings): void {
	if (typeof document === "undefined") return;
	const normalized = normalizeAppearanceSettings(settings);
	const value = encodeURIComponent(JSON.stringify(normalized));
	const secure = typeof window !== "undefined" && window.location.protocol === "https:" ? "; Secure" : "";
	document.cookie = `${APPEARANCE_COOKIE}=${value}; Max-Age=${COOKIE_MAX_AGE_SECONDS}; Path=/; SameSite=Lax${secure}`;
}

export function clearAppearanceCookie(): void {
	if (typeof document === "undefined") return;
	document.cookie = `${APPEARANCE_COOKIE}=; Max-Age=0; Path=/; SameSite=Lax`;
}

export { defaultAppearanceSettings };
