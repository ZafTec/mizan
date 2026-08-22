"use client";

import { createContext, useCallback, useContext, useMemo, type ReactNode } from "react";
import { useRouter } from "next/navigation";
import type { User } from "@/lib/auth";

interface SessionValue {
	data: { user: User } | null;
	isPending: boolean;
	refresh: () => void;
}

const SessionContext = createContext<SessionValue>({
	data: null,
	isPending: false,
	refresh: () => {},
});

/**
 * The root layout already resolves the user server-side, so the client reads it
 * from context instead of fetching. Replaces BetterAuth's useSession without
 * adding a request - see docs/REFOCUS.md §6.
 */
export function SessionProvider({ user, children }: { user: User | null; children: ReactNode }) {
	const router = useRouter();
	const refresh = useCallback(() => router.refresh(), [router]);
	const value = useMemo<SessionValue>(
		() => ({ data: user ? { user } : null, isPending: false, refresh }),
		[user, refresh],
	);

	return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>;
}

export function useSession(): SessionValue {
	return useContext(SessionContext);
}
