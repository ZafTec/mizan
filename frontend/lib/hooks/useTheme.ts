"use client";

import { useState, useEffect, useCallback, useMemo } from "react";
import { authClient, useSession } from "@/lib/auth-client";
import {
	applyAppearanceSettings,
	type AppearanceUserFields,
	defaultAppearanceSettings,
	getAppearanceSettingsFromUser,
	getStoredAppearanceSettings,
	normalizeAppearanceSettings,
	type AppearanceSettings,
} from "@/lib/appearance";

export function useTheme() {
    const { data: session } = useSession();
    const [draftSettings, setDraftSettings] = useState<AppearanceSettings | null>(null);
    const sessionAppearance = useMemo(
		() => getAppearanceSettingsFromUser(session?.user as AppearanceUserFields | null | undefined),
		[session?.user]
	);
    const settings = draftSettings ?? (session?.user ? sessionAppearance : getStoredAppearanceSettings());

    useEffect(() => {
		applyAppearanceSettings(settings);
    }, [settings]);

	useEffect(() => {
		if (settings.theme !== "system") {
			return;
		}

		const mediaQuery = window.matchMedia("(prefers-color-scheme: dark)");
		const handleChange = () => applyAppearanceSettings(settings);
		mediaQuery.addEventListener("change", handleChange);
		return () => mediaQuery.removeEventListener("change", handleChange);
	}, [settings]);

	const updateSettings = useCallback((updates: Partial<AppearanceSettings>) => {
		setDraftSettings((prev) => {
			const next = normalizeAppearanceSettings({ ...(prev ?? settings), ...updates });
			applyAppearanceSettings(next);
			return next;
		});
	}, [settings]);

	const persistSettings = useCallback(async (updates?: Partial<AppearanceSettings>) => {
		const next = normalizeAppearanceSettings({ ...settings, ...updates });
		setDraftSettings(next);
		applyAppearanceSettings(next);
		await authClient.updateUser({
			themePreference: next.theme,
			compactMode: next.compactMode,
			reduceAnimations: next.reduceAnimations,
		} as never);
		return next;
	}, [settings]);

	return {
		settings: settings ?? defaultAppearanceSettings,
		updateSettings,
		persistSettings,
	};
}
