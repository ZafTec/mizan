"use client";

import { useEffect, useRef, useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import { Icon } from "@/components/ui/icon";
import AiMarkdown from "@/components/ai/AiMarkdown";
import {
	getAiConsent,
	getOnboardingThread,
	listOnboardingTools,
	sendOnboardingMessage,
	type AiChatMessage,
	type AiConsent,
	type AiToolInvocation,
	type AiToolSummary,
} from "@/lib/api/ai";
import AiPermissionGate from "@/components/ai/AiPermissionGate";
import { cn } from "@/lib/utils";

const OPENER =
	"Hi. I can set you up in a couple of minutes - what are you hoping to get out of tracking?";

interface Entry {
	message: AiChatMessage;
	performed?: AiToolInvocation[];
}

/**
 * Setup as a conversation instead of a six-screen form (docs/REFOCUS.md §10).
 *
 * The difference from chat is that this model has tools, so every turn says
 * what it actually did. That echo is not decoration: a model recording things
 * on your behalf without telling you is indistinguishable from one making
 * things up.
 */
export default function OnboardingAgent() {
	const router = useRouter();
	const [tools, setTools] = useState<AiToolSummary[]>([]);
	const [threadId, setThreadId] = useState<string | null>(null);
	const [entries, setEntries] = useState<Entry[]>([]);
	const [input, setInput] = useState("");
	const [error, setError] = useState<string | null>(null);
	const [pending, startTransition] = useTransition();
	const [anythingDone, setAnythingDone] = useState(false);
	const [resuming, setResuming] = useState(true);
	// Undefined until we know. Setup writes to the log, so it has to ask before
	// it can begin rather than failing on the first tool call.
	const [consent, setConsent] = useState<AiConsent | null>(null);
	const scroller = useRef<HTMLDivElement>(null);

	useEffect(() => {
		getAiConsent()
			.then(setConsent)
			.catch(() => setConsent(null));
	}, []);

	useEffect(() => {
		listOnboardingTools()
			.then(setTools)
			.catch(() => setTools([]));
	}, []);

	// Setup is one conversation, not one per visit. Without this the page came
	// back blank and the next turn started a fresh thread, so the model asked
	// again for everything it had already been told.
	useEffect(() => {
		let cancelled = false;

		getOnboardingThread()
			.then((thread) => {
				if (cancelled || !thread) return;
				setThreadId(thread.id);
				// What each turn *did* is not persisted, only what was said, so a
				// resumed conversation shows the words without the action echoes.
				setEntries(thread.messages.map((message) => ({ message })));
				scrollToBottom();
			})
			.catch(() => {
				// A failed resume is not a failed setup - the conversation still
				// works, it just starts from the greeting.
			})
			.finally(() => {
				if (!cancelled) setResuming(false);
			});

		return () => {
			cancelled = true;
		};
	}, []);

	function scrollToBottom() {
		requestAnimationFrame(() => {
			scroller.current?.scrollTo({
				top: scroller.current.scrollHeight,
				behavior: "smooth",
			});
		});
	}

	function send(text: string) {
		const trimmed = text.trim();
		if (!trimmed || pending) return;

		const optimisticId = `pending-${crypto.randomUUID()}`;
		setEntries((current) => [
			...current,
			{
				message: {
					id: optimisticId,
					fromUser: true,
					content: trimmed,
					createdAt: new Date().toISOString(),
				},
			},
		]);
		setInput("");
		setError(null);
		scrollToBottom();

		startTransition(async () => {
			try {
				const turn = await sendOnboardingMessage(threadId, trimmed);
				setThreadId(turn.threadId);
				setEntries((current) => [...current, { message: turn.reply, performed: turn.performed }]);
				if (turn.performed.some((p) => p.succeeded)) {
					setAnythingDone(true);
					// Something was written, so anything already on screen
					// behind this is stale.
					router.refresh();
				}
				scrollToBottom();
			} catch (err) {
				setError(err instanceof Error ? err.message : "That did not go through.");
				setEntries((current) => current.filter((entry) => entry.message.id !== optimisticId));
				setInput(trimmed);
			}
		});
	}

	// Never asked, and never granted anything: ask before the first turn rather
	// than letting every tool call come back refused.
	const needsPermission =
		consent !== null && !consent.allowWrites && !consent.enabled && entries.length === 0;

	if (needsPermission) {
		return <AiPermissionGate consent={consent} onGranted={setConsent} />;
	}

	return (
		<div className="space-y-4">
			<div ref={scroller} className="glass-panel max-h-[60vh] space-y-4 overflow-y-auto p-5">
				<Bubble fromUser={false}>{OPENER}</Bubble>

				{entries.map((entry) => (
					<div key={entry.message.id} className="space-y-2">
						<Bubble fromUser={entry.message.fromUser}>
							{entry.message.fromUser ? (
								entry.message.content
							) : (
								<AiMarkdown content={entry.message.content} />
							)}
						</Bubble>
						{entry.performed && entry.performed.length > 0 && (
							<ul className="ml-12 space-y-1">
								{entry.performed.map((action, i) => (
									<li
										key={`${action.tool}-${i}`}
										className={cn(
											"flex items-start gap-2 text-xs",
											action.succeeded
												? "text-verdigris-700 dark:text-verdigris-300"
												: "text-amber-700 dark:text-amber-300",
										)}
									>
										<Icon name={action.succeeded ? "circleCheck" : "badgeAlert"} size={13} />
										<span>{action.succeeded ? action.summary : action.error}</span>
									</li>
								))}
							</ul>
						)}
					</div>
				))}

				{pending && (
					<Bubble fromUser={false}>
						<span className="inline-flex items-center gap-1.5">
							<span className="h-2 w-2 animate-pulse rounded-full bg-verdigris-500" />
							<span
								className="h-2 w-2 animate-pulse rounded-full bg-verdigris-500"
								style={{ animationDelay: "120ms" }}
							/>
							<span
								className="h-2 w-2 animate-pulse rounded-full bg-verdigris-500"
								style={{ animationDelay: "240ms" }}
							/>
						</span>
					</Bubble>
				)}

				{error && (
					<div className="rounded-2xl border border-red-300 bg-red-50 px-4 py-3 text-sm text-red-700 dark:border-red-500/30 dark:bg-red-500/10 dark:text-red-300">
						{error}
					</div>
				)}
			</div>

			<form
				onSubmit={(e) => {
					e.preventDefault();
					send(input);
				}}
				className="flex items-center gap-2"
			>
				<input
					value={input}
					onChange={(e) => setInput(e.target.value)}
					disabled={pending || resuming}
					placeholder={resuming ? "Picking up where you left off…" : "Tell it what you're after…"}
					className="input flex-1 !rounded-2xl !py-3"
					autoComplete="off"
				/>
				<button
					type="submit"
					disabled={pending || resuming || !input.trim()}
					className="btn-primary !rounded-2xl !py-3"
					aria-label="Send"
				>
					<Icon name="arrowRight" size={16} />
				</button>
			</form>

			{tools.length > 0 && (
				<details className="rounded-2xl border border-charcoal-blue-200 p-4 text-xs dark:border-white/10">
					<summary className="cursor-pointer text-charcoal-blue-600 dark:text-charcoal-blue-300">
						What this can do
					</summary>
					<ul className="mt-2 space-y-1 text-charcoal-blue-500 dark:text-charcoal-blue-400">
						{tools.map((tool) => (
							<li key={tool.name}>
								<code className="text-charcoal-blue-700 dark:text-charcoal-blue-200">
									{tool.name}
								</code>{" "}
								— {tool.description}
							</li>
						))}
					</ul>
					<p className="mt-2 text-charcoal-blue-400 dark:text-charcoal-blue-500">
						That is the whole list. It cannot delete anything, and it only ever acts on your own
						records.
					</p>
				</details>
			)}

			{anythingDone && (
				<p className="text-center text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
					Done for now?{" "}
					<a
						href="/dashboard"
						className="text-verdigris-600 hover:underline dark:text-verdigris-400"
					>
						Go to your dashboard
					</a>
				</p>
			)}
		</div>
	);
}

function Bubble({ fromUser, children }: { fromUser: boolean; children: React.ReactNode }) {
	return (
		<div className={cn("flex gap-3", fromUser ? "flex-row-reverse" : "flex-row")}>
			<span
				className={cn(
					"flex h-9 w-9 shrink-0 items-center justify-center rounded-2xl",
					fromUser
						? "bg-verdigris-600 text-white"
						: "bg-white text-verdigris-700 ring-1 ring-charcoal-blue-200 dark:bg-charcoal-blue-950 dark:text-verdigris-300 dark:ring-white/10",
				)}
			>
				<Icon name={fromUser ? "user" : "bot"} size={14} />
			</span>
			<div
				className={cn(
					"max-w-[78%] rounded-3xl px-4 py-3 text-sm leading-relaxed",
					fromUser
						? "whitespace-pre-wrap bg-verdigris-600 text-white "
						: "bg-white text-charcoal-blue-900 ring-1 ring-charcoal-blue-200 dark:bg-charcoal-blue-950/80 dark:text-charcoal-blue-100 dark:ring-white/10",
				)}
			>
				{children}
			</div>
		</div>
	);
}
