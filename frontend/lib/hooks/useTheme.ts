"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { authClient, useSession } from "@/lib/auth-client";
import {
	applyAppearanceClasses,
	type AppearanceSettings,
	type AppearanceUserFields,
	defaultAppearanceSettings,
	getAppearanceSettingsFromUser,
	normalizeAppearanceSettings,
} from "@/lib/appearance";
import {
	readAppearanceCookie,
	writeAppearanceCookie,
} from "@/lib/appearance-cookie";

/**
 * Single source of truth: local state. Hydration order is
 *   1. cookie (most recent local intent, written on every change)
 *   2. user record (server-persisted)
 *   3. default
 * A saved change writes cookie + user record together, so the two can't drift.
 */
export function useTheme() {
	const { data: session } = useSession();
	const [settings, setSettings] = useState<AppearanceSettings>(defaultAppearanceSettings);
	const hydratedRef = useRef(false);

	// Hydrate once on mount, and again when the session user changes.
	// The cookie is already applied by the pre-hydration <script>; this just lifts
	// the same value into React state so the settings form can edit it.
	useEffect(() => {
		const cookie = readAppearanceCookie();
		const fromUser = getAppearanceSettingsFromUser(
			session?.user as AppearanceUserFields | null | undefined
		);
		const next = cookie ?? fromUser;
		// eslint-disable-next-line react-hooks/set-state-in-effect -- one-time read from cookie/session
		setSettings(next);
		applyAppearanceClasses(next);
		hydratedRef.current = true;
	}, [session?.user]);

	// Keep in sync with OS preference changes when following system.
	useEffect(() => {
		if (settings.theme !== "system" || typeof window === "undefined") return;
		const media = window.matchMedia("(prefers-color-scheme: dark)");
		const handler = () => applyAppearanceClasses(settings);
		media.addEventListener("change", handler);
		return () => media.removeEventListener("change", handler);
	}, [settings]);

	const updateSettings = useCallback((updates: Partial<AppearanceSettings>) => {
		setSettings((prev) => {
			const next = normalizeAppearanceSettings({ ...prev, ...updates });
			applyAppearanceClasses(next);
			writeAppearanceCookie(next);
			return next;
		});
	}, []);

	const persistSettings = useCallback(
		async (updates?: Partial<AppearanceSettings>): Promise<AppearanceSettings> => {
			const next = normalizeAppearanceSettings({ ...settings, ...updates });
			setSettings(next);
			applyAppearanceClasses(next);
			writeAppearanceCookie(next);
			await authClient.updateUser({
				themePreference: next.theme,
				compactMode: next.compactMode,
				reduceAnimations: next.reduceAnimations,
			} as never);
			return next;
		},
		[settings]
	);

	return {
		settings,
		updateSettings,
		persistSettings,
	};
}
