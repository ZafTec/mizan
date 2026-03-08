"use client";

import { useEffect, useMemo } from "react";
import { useSession } from "@/lib/auth-client";
import {
	applyAppearanceSettings,
	type AppearanceUserFields,
	getAppearanceSettingsFromUser,
	getStoredAppearanceSettings,
} from "@/lib/appearance";

export function AppearanceSync() {
	const { data: session } = useSession();
	const sessionAppearance = useMemo(
		() => getAppearanceSettingsFromUser(session?.user as AppearanceUserFields | null | undefined),
		[session?.user]
	);

	useEffect(() => {
		applyAppearanceSettings(getStoredAppearanceSettings());
	}, []);

	useEffect(() => {
		if (!session?.user) {
			return;
		}

		applyAppearanceSettings(sessionAppearance);
	}, [session?.user, sessionAppearance]);

	return null;
}
