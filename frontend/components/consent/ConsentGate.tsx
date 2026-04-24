"use client";

import { useCallback, useState, useSyncExternalStore } from "react";
import { GoogleAnalytics } from "@next/third-parties/google";
import { CookieConsentBanner } from "./CookieConsentBanner";

const CONSENT_COOKIE = "mizan-consent";

type ConsentState = "pending" | "accepted" | "declined";

function readConsentCookie(): ConsentState {
	if (typeof document === "undefined") return "pending";
	const match = document.cookie
		.split("; ")
		.find((row) => row.startsWith(`${CONSENT_COOKIE}=`));
	if (!match) return "pending";
	const value = match.split("=")[1];
	return value === "accepted" ? "accepted" : value === "declined" ? "declined" : "pending";
}

function writeConsentCookie(value: "accepted" | "declined") {
	if (typeof document === "undefined") return;
	const oneYear = 60 * 60 * 24 * 365;
	const secure = window.location.protocol === "https:" ? "; Secure" : "";
	document.cookie = `${CONSENT_COOKIE}=${value}; path=/; max-age=${oneYear}; SameSite=Lax${secure}`;
}

// useSyncExternalStore keeps React in sync with the cookie without tripping
// the lint rule that bans setState-in-effect. Cookies don't fire change
// events, so subscribe is a no-op — we only need the initial snapshot plus
// a way to force-refresh when the user clicks Accept/Decline.
const subscribe = () => () => {};

export function ConsentGate({ gaId }: { gaId?: string | null }) {
	// Shortcut: no GA ID configured means no tracking ever happens, no
	// banner needed, no script to load. The entire consent layer is dark.
	const trackingAvailable = Boolean(gaId && gaId.trim().length > 0);

	const cookieConsent = useSyncExternalStore<ConsentState>(
		subscribe,
		readConsentCookie,
		() => "pending",
	);

	// Tracks the user's in-session decision so we re-render immediately
	// without waiting for a cookie read round-trip.
	const [override, setOverride] = useState<ConsentState | null>(null);
	const consent = override ?? cookieConsent;

	const handleDecision = useCallback((decision: "accepted" | "declined") => {
		writeConsentCookie(decision);
		setOverride(decision);
	}, []);

	if (!trackingAvailable) return null;

	return (
		<>
			{consent === "accepted" && <GoogleAnalytics gaId={gaId!} />}
			{consent === "pending" && <CookieConsentBanner onDecision={handleDecision} />}
		</>
	);
}
