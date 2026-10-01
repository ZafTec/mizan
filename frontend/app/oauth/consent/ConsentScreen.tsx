"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { Icon } from "@/components/ui/icon";
import { Skeleton } from "@/components/ui/skeleton";
import { HouseholdPicker, PermissionPicker } from "@/components/mcp/PermissionPicker";
import { McpApiError, oauthConsentApi } from "@/lib/api/mcp";
import {
	defaultLevels,
	levelsFromScopes,
	scopesFromLevels,
	type AccessLevels,
	type HouseholdMode,
} from "@/lib/mcp-permissions";
import type { AuthorizationRequest } from "@/types/mcp";

/**
 * Where a person decides what a connecting app may do. The app asked for
 * something, but nothing it asked for is granted until the person says so
 * here, one permission group at a time, and the household choice is made on
 * the same screen so it cannot be forgotten.
 */
export function ConsentScreen() {
	const router = useRouter();
	const params = useSearchParams();
	const secret = params.get("request");
	const [request, setRequest] = useState<AuthorizationRequest | null>(null);
	const [problem, setProblem] = useState<string | null>(null);
	const [levels, setLevels] = useState<AccessLevels>({});
	const [mode, setMode] = useState<HouseholdMode>("none");
	const [selected, setSelected] = useState<string[]>([]);
	const [busy, setBusy] = useState<"allow" | "deny" | null>(null);

	useEffect(() => {
		if (!secret) return;

		let cancelled = false;
		oauthConsentApi
			.view(secret)
			.then((view) => {
				if (cancelled) return;
				setRequest(view);

				const groups = view.scopeGroups ?? [];
				const existing = view.existingGrant;
				setLevels(
					existing
						? levelsFromScopes(groups, existing.scopes ?? [])
						: defaultLevels(groups, view.requestedScopes ?? []),
				);
				if (existing) {
					setMode(existing.householdMode as HouseholdMode);
					setSelected(existing.householdIds ?? []);
				}
			})
			.catch((error: unknown) => {
				if (cancelled) return;
				// The cookie is there but the session is not valid any more. Sign in, then come back.
				if (error instanceof McpApiError && error.status === 401) {
					const back = `${window.location.pathname}${window.location.search}`;
					router.replace(`/login?callbackUrl=${encodeURIComponent(back)}`);
					return;
				}
				setProblem(
					error instanceof McpApiError && error.status === 404
						? "This request has expired. Start again from the app."
						: error instanceof McpApiError
							? error.message
							: "Could not load this request.",
				);
			});

		return () => {
			cancelled = true;
		};
	}, [secret, router]);

	const groups = useMemo(() => request?.scopeGroups ?? [], [request]);
	const scopes = useMemo(() => scopesFromLevels(groups, levels), [groups, levels]);
	const requested = useMemo(() => request?.requestedScopes ?? [], [request]);
	const asked = useMemo(() => levelsFromScopes(groups, requested), [groups, requested]);

	async function decide(approve: boolean) {
		if (!secret || !request) return;
		setBusy(approve ? "allow" : "deny");
		setProblem(null);

		try {
			const result = await oauthConsentApi.decide(
				secret,
				approve
					? {
							approve,
							scopes: request.isFirstParty ? (requested.length > 0 ? requested : ["full"]) : scopes,
							householdMode: mode,
							householdIds: selected,
						}
					: { approve },
			);
			if (!result.redirectUrl) throw new McpApiError(500, "The server did not say where to send you back.");
			// A full navigation: the address may be an app link such as com.example:/callback.
			window.location.assign(result.redirectUrl);
		} catch (error) {
			setBusy(null);
			setProblem(error instanceof McpApiError ? error.message : "Could not save your choice. Try again.");
		}
	}

	const shownProblem = secret ? problem : "This page needs a connection request. Start again from the app.";

	if (shownProblem && !request) {
		return (
			<div className="mx-auto max-w-xl space-y-4 py-10">
				<h1 className="text-2xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50">
					This request cannot continue
				</h1>
				<p role="alert" className="text-sm text-destructive">{shownProblem}</p>
				<Link href="/profile/mcp" className="btn-secondary inline-flex">Go to connected apps</Link>
			</div>
		);
	}

	if (!request) return <Skeleton className="mx-auto h-96 max-w-xl" />;

	const nothingChosen = !request.isFirstParty && scopes.length === 0;

	return (
		<div className="mx-auto max-w-2xl space-y-6 pb-10">
			<header className="space-y-2">
				<p className="eyebrow">Connect an app</p>
				<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50">
					{request.clientName} wants to connect to Mizan
				</h1>
				<Trust request={request} />
			</header>

			{request.isFirstParty ? (
				<p className="text-sm text-charcoal-blue-600 dark:text-charcoal-blue-300">
					This is the Mizan app. Allowing it gives it the same access you have when you are signed in here.
				</p>
			) : (
				<>
					<section aria-labelledby="permissions-heading" className="card p-4 sm:p-5">
						<h2 id="permissions-heading" className="text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100">
							What it may do
						</h2>
						<p className="mb-2 text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
							Choose for each area. Anything you leave at no access stays out of reach.
						</p>
						<PermissionPicker groups={groups} levels={levels} onChange={setLevels} ceiling={requested.length > 0 ? asked : undefined} />
					</section>

					{(request.households ?? []).length > 0 && (
						<section className="card p-4 sm:p-5">
							<HouseholdPicker
								households={(request.households ?? []).map((h) => ({ id: h.id!, name: h.name! }))}
								mode={mode}
								selected={selected}
								onChange={(nextMode, ids) => {
									setMode(nextMode);
									setSelected(ids);
								}}
							/>
						</section>
					)}
				</>
			)}

			{problem && <p role="alert" className="text-sm text-destructive">{problem}</p>}

			<div className="flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
				<button type="button" className="btn-secondary" disabled={busy !== null} onClick={() => void decide(false)}>
					{busy === "deny" ? "Denying…" : "Deny"}
				</button>
				<button
					type="button"
					className="btn-primary"
					disabled={busy !== null || nothingChosen}
					onClick={() => void decide(true)}
				>
					{busy === "allow" ? "Allowing…" : "Allow"}
				</button>
			</div>

			<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
				You can change or remove this any time in More, then Connected apps. After you choose, you return to{" "}
				<span className="font-medium">{request.redirectHost}</span>.
			</p>
		</div>
	);
}

function Trust({ request }: { request: AuthorizationRequest }) {
	if (request.isFirstParty) {
		return (
			<p className="flex items-center gap-2 text-sm text-charcoal-blue-600 dark:text-charcoal-blue-300">
				<Icon name="shieldCheck" size={16} /> A Mizan app
			</p>
		);
	}

	if (request.source === "metadata" && request.verifiedHost) {
		return (
			<p className="flex items-center gap-2 text-sm text-charcoal-blue-600 dark:text-charcoal-blue-300">
				<Icon name="circleCheck" size={16} /> Published by {request.verifiedHost}
			</p>
		);
	}

	return (
		<p className="flex items-start gap-2 text-sm text-charcoal-blue-600 dark:text-charcoal-blue-300">
			<Icon name="badgeAlert" size={16} className="mt-0.5" />
			<span>
				This app registered itself and Mizan cannot confirm who made it. Only continue if you started this connection
				yourself.
			</span>
		</p>
	);
}
