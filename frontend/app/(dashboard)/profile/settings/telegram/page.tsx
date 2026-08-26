"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import Link from "next/link";
import { AnimatedIcon } from "@/components/ui/animated-icon";
import { Skeleton } from "@/components/ui/skeleton";
import ConfirmationModal from "@/components/ConfirmationModal";
import { appToast } from "@/lib/toast";
import {
	getTelegramLink,
	issueTelegramCode,
	unlinkTelegram,
	type TelegramLink,
	type TelegramLinkCode,
} from "@/lib/api/telegram";

/**
 * Connecting Telegram, as three steps and a page that watches.
 *
 * The guided part is the watching: after the code is issued the page polls
 * until the bot spends it, so the last step is "it says connected" rather than
 * "come back and refresh to find out". A code lasts five minutes, and the
 * countdown says so instead of letting it silently go stale.
 */
export default function TelegramSettingsPage() {
	const [link, setLink] = useState<TelegramLink | null>(null);
	const [code, setCode] = useState<TelegramLinkCode | null>(null);
	const [loading, setLoading] = useState(true);
	const [issuing, setIssuing] = useState(false);
	const [unlinking, setUnlinking] = useState(false);
	const [confirmUnlink, setConfirmUnlink] = useState(false);
	const [remaining, setRemaining] = useState<string | null>(null);

	const waiting = code !== null && link?.linked !== true;
	const waitingRef = useRef(waiting);
	waitingRef.current = waiting;

	const load = useCallback(async () => {
		try {
			return await getTelegramLink();
		} catch (error) {
			appToast.error(error, "Could not load your Telegram settings");
			return null;
		}
	}, []);

	useEffect(() => {
		let cancelled = false;

		void load().then((next) => {
			if (cancelled) return;
			setLink(next);
			setLoading(false);
		});

		return () => {
			cancelled = true;
		};
	}, [load]);

	// Two timers, both only while a code is outstanding: one asks the API
	// whether the bot has spent it, the other counts the code down.
	useEffect(() => {
		if (!waiting) return;

		let cancelled = false;
		const poll = setInterval(async () => {
			const next = await load();
			if (cancelled || next === null) return;

			setLink(next);
			if (next.linked) {
				setCode(null);
				appToast.success("Telegram connected");
			}
		}, 3000);

		return () => {
			cancelled = true;
			clearInterval(poll);
		};
	}, [waiting, load]);

	useEffect(() => {
		if (code === null) {
			setRemaining(null);
			return;
		}

		const tick = () => {
			const left = new Date(code.expiresAt).getTime() - Date.now();

			if (left <= 0) {
				setRemaining(null);
				setCode(null);
				return;
			}

			const seconds = Math.floor(left / 1000);
			setRemaining(`${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, "0")}`);
		};

		tick();
		const timer = setInterval(tick, 1000);
		return () => clearInterval(timer);
	}, [code]);

	async function onConnect() {
		setIssuing(true);
		try {
			setCode(await issueTelegramCode());
		} catch (error) {
			appToast.error(error, "Could not start the connection");
		} finally {
			setIssuing(false);
		}
	}

	async function onUnlink() {
		setUnlinking(true);
		try {
			await unlinkTelegram();
			setCode(null);
			setLink(await load());
			appToast.success("Telegram disconnected");
		} catch (error) {
			appToast.error(error, "Could not disconnect");
		} finally {
			setUnlinking(false);
			setConfirmUnlink(false);
		}
	}

	if (loading) {
		return (
			<div className="mx-auto max-w-2xl space-y-6 py-8">
				<Skeleton className="h-9 w-56" />
				<Skeleton className="h-48 w-full rounded-3xl" />
			</div>
		);
	}

	return (
		<div className="mx-auto max-w-2xl space-y-8 py-8">
			<header className="space-y-2">
				<Link
					href="/profile/settings"
					className="inline-flex items-center gap-1 text-sm text-charcoal-blue-500 hover:underline dark:text-charcoal-blue-400"
				>
					← Settings
				</Link>
				<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50">
					Telegram
				</h1>
				<p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
					Log meals by sending a photo, and ask the assistant anything — it is the
					same conversation you have here.
				</p>
			</header>

			{link?.botConfigured !== true ? (
				<NotConfigured />
			) : link.linked ? (
				<Connected link={link} onDisconnect={() => setConfirmUnlink(true)} />
			) : (
				<Connect
					code={code}
					remaining={remaining}
					issuing={issuing}
					botUsername={link.botUsername}
					onConnect={onConnect}
					onCancel={() => setCode(null)}
				/>
			)}

			<ConfirmationModal
				isOpen={confirmUnlink}
				onClose={() => setConfirmUnlink(false)}
				onConfirm={onUnlink}
				title="Disconnect Telegram?"
				message="The bot stops responding in that chat. Nothing you have logged is affected, and you can reconnect any time."
				confirmText="Disconnect"
				isLoading={unlinking}
				isDanger
			/>
		</div>
	);
}

function NotConfigured() {
	return (
		<div className="surface-panel space-y-2 p-6">
			<h2 className="text-base font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
				Not available on this server
			</h2>
			<p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
				No Telegram bot is configured here. If you run this instance, set{" "}
				<code className="rounded bg-charcoal-blue-100 px-1 py-0.5 text-xs dark:bg-white/10">
					TELEGRAM_BOT_TOKEN
				</code>{" "}
				and{" "}
				<code className="rounded bg-charcoal-blue-100 px-1 py-0.5 text-xs dark:bg-white/10">
					TELEGRAM_BOT_USERNAME
				</code>
				.
			</p>
		</div>
	);
}

function Connected({ link, onDisconnect }: { link: TelegramLink; onDisconnect: () => void }) {
	return (
		<div className="space-y-4">
			<div className="surface-panel flex flex-wrap items-center justify-between gap-4 p-6">
				<div className="space-y-1">
					<p className="flex items-center gap-2 font-medium text-charcoal-blue-900 dark:text-charcoal-blue-50">
						<span className="h-2 w-2 rounded-full bg-verdigris-500" />
						Connected
						{link.telegramUsername && (
							<span className="text-charcoal-blue-500 dark:text-charcoal-blue-400">
								as @{link.telegramUsername}
							</span>
						)}
					</p>
					<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
						{link.lastSeenAt
							? `Last message ${new Date(link.lastSeenAt).toLocaleString()}`
							: `Linked ${link.linkedAt ? new Date(link.linkedAt).toLocaleDateString() : ""}`}
					</p>
				</div>

				<button type="button" onClick={onDisconnect} className="btn-ghost btn-sm text-red-600 dark:text-red-400">
					Disconnect
				</button>
			</div>

			<div className="surface-panel space-y-3 p-6">
				<h2 className="text-sm font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
					What to send it
				</h2>
				<ul className="space-y-2 text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
					<li>
						<strong className="text-charcoal-blue-700 dark:text-charcoal-blue-200">A photo</strong>{" "}
						— it estimates the meal and asks you to confirm before logging anything.
					</li>
					<li>
						<strong className="text-charcoal-blue-700 dark:text-charcoal-blue-200">/today</strong>{" "}
						— your totals against your targets.
					</li>
					<li>
						<strong className="text-charcoal-blue-700 dark:text-charcoal-blue-200">/weight 82.4</strong>{" "}
						— a weigh-in.
					</li>
					<li>
						<strong className="text-charcoal-blue-700 dark:text-charcoal-blue-200">Anything else</strong>{" "}
						— goes to the assistant, on the same thread as this site.
					</li>
				</ul>
			</div>
		</div>
	);
}

function Connect({
	code,
	remaining,
	issuing,
	botUsername,
	onConnect,
	onCancel,
}: {
	code: TelegramLinkCode | null;
	remaining: string | null;
	issuing: boolean;
	botUsername: string | null;
	onConnect: () => void;
	onCancel: () => void;
}) {
	if (code === null) {
		return (
			<div className="surface-panel space-y-4 p-6">
				<div className="space-y-1">
					<h2 className="text-base font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
						Connect your Telegram
					</h2>
					<p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
						Three steps, about twenty seconds. You will need Telegram installed on
						the device you want to log from.
					</p>
				</div>

				<ol className="space-y-3 text-sm">
					<Step n={1}>Tap the button below — it opens a one-time link.</Step>
					<Step n={2}>
						Telegram opens on {botUsername ? <>@{botUsername}</> : "the bot"}. Tap{" "}
						<strong>Start</strong>.
					</Step>
					<Step n={3}>Come back here. This page will say connected.</Step>
				</ol>

				<button type="button" onClick={onConnect} disabled={issuing} className="btn-primary w-full sm:w-auto">
					{issuing ? "Preparing…" : "Connect Telegram"}
				</button>
			</div>
		);
	}

	return (
		<div className="surface-panel space-y-5 p-6">
			<div className="flex items-start justify-between gap-4">
				<div className="space-y-1">
					<h2 className="text-base font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
						Waiting for Telegram
					</h2>
					<p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
						Open the link and tap <strong>Start</strong>. This page notices on its own.
					</p>
				</div>

				{remaining && (
					<span className="whitespace-nowrap rounded-full bg-amber-100 px-2.5 py-1 text-xs font-medium tabular-nums text-amber-800 dark:bg-amber-500/15 dark:text-amber-300">
						{remaining} left
					</span>
				)}
			</div>

			<a href={code.deepLink} target="_blank" rel="noopener noreferrer" className="btn-primary w-full sm:w-auto">
				Open Telegram
			</a>

			<div className="space-y-2 border-t border-charcoal-blue-100 pt-4 dark:border-white/10">
				<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
					Telegram is on another device? Copy this link and open it there.
				</p>
				<CopyRow value={code.deepLink} />
			</div>

			<div className="flex items-center gap-3 text-sm">
				<span className="inline-flex h-2 w-2 animate-pulse rounded-full bg-verdigris-500" />
				<span className="text-charcoal-blue-500 dark:text-charcoal-blue-400">Watching for the connection…</span>
				<button type="button" onClick={onCancel} className="ml-auto text-xs text-charcoal-blue-500 hover:underline">
					Cancel
				</button>
			</div>
		</div>
	);
}

function Step({ n, children }: { n: number; children: React.ReactNode }) {
	return (
		<li className="flex gap-3">
			<span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-verdigris-100 text-xs font-semibold text-verdigris-800 dark:bg-verdigris-500/20 dark:text-verdigris-300">
				{n}
			</span>
			<span className="pt-0.5 text-charcoal-blue-600 dark:text-charcoal-blue-300">{children}</span>
		</li>
	);
}

function CopyRow({ value }: { value: string }) {
	const [copied, setCopied] = useState(false);

	async function copy() {
		try {
			await navigator.clipboard.writeText(value);
			setCopied(true);
			setTimeout(() => setCopied(false), 2000);
		} catch {
			appToast.error("Could not copy. Select the link and copy it manually.");
		}
	}

	return (
		<div className="flex items-center gap-2">
			<code className="min-w-0 flex-1 truncate rounded-xl bg-charcoal-blue-100 px-3 py-2 text-xs text-charcoal-blue-700 dark:bg-white/5 dark:text-charcoal-blue-200">
				{value}
			</code>
			<button type="button" onClick={copy} className="btn-ghost btn-sm shrink-0" aria-label="Copy link">
				{copied ? <AnimatedIcon name="circleCheck" size={14} /> : "Copy"}
			</button>
		</div>
	);
}
