"use client";

import { useEffect, useRef } from "react";
import { useSession } from "@/lib/auth-client";
import {
	applyAppearanceClasses,
	type AppearanceUserFields,
	getAppearanceSettingsFromUser,
} from "@/lib/appearance";
import {
	readAppearanceCookie,
	writeAppearanceCookie,
} from "@/lib/appearance-cookie";

/**
 * Reconciles the active appearance on every route. The pre-hydration <script>
 * in <head> already applied the right classes from the cookie, so this is only
 * responsible for:
 *
 *  1. Writing a cookie for first-time visitors (so SSR can paint correctly next time).
 *  2. Pulling the user's persisted preference when they log in on a new device
 *     (cookie is empty → user record wins).
 *  3. Keeping `(prefers-color-scheme)` listeners alive when in system mode.
 */
export function AppearanceSync() {
	const { data: session } = useSession();
	const bootedRef = useRef(false);

	useEffect(() => {
		const cookie = readAppearanceCookie();
		const fromUser = getAppearanceSettingsFromUser(
			session?.user as AppearanceUserFields | null | undefined
		);

		// If a logged-in user visits from a fresh device (no cookie), seed it from their record.
		// Otherwise keep the cookie: it represents the most recent local intent.
		const active = cookie ?? fromUser;
		applyAppearanceClasses(active);
		if (!cookie) writeAppearanceCookie(active);

		bootedRef.current = true;
	}, [session?.user]);

	useEffect(() => {
		if (typeof window === "undefined") return;
		const media = window.matchMedia("(prefers-color-scheme: dark)");
		const handler = () => {
			const current = readAppearanceCookie();
			if (current?.theme === "system") applyAppearanceClasses(current);
		};
		media.addEventListener("change", handler);
		return () => media.removeEventListener("change", handler);
	}, []);

	return null;
}
