"use client";

import { useState, useTransition } from "react";
import { Icon } from "@/components/ui/icon";
import { askAboutClient, type AiChatMessage } from "@/lib/api/ai";
import { cn } from "@/lib/utils";

const AXIS_LABELS: Record<string, string> = {
	nutrition: "nutrition",
	training: "training",
	body: "measurements",
};

/**
 * Advisory, and said so rather than implied (docs/REFOCUS.md §11).
 *
 * Two things are load-bearing here and neither is decoration. The panel names
 * what the client actually shared, because "your client's protein is low" and
 * "your client has not shared their nutrition" are answers a coach must be
 * able to tell apart. And nothing here writes to the client's record - the
 * backend offers this surface no tools at all, so there is no button to add.
 */
export default function ClientAiPanel({ clientId }: { clientId: string }) {
	const [threadId, setThreadId] = useState<string | null>(null);
	const [messages, setMessages] = useState<AiChatMessage[]>([]);
	const [axesSeen, setAxesSeen] = useState<string[] | null>(null);
	const [input, setInput] = useState("");
	const [error, setError] = useState<string | null>(null);
	const [pending, startTransition] = useTransition();

	function send(text: string) {
		const trimmed = text.trim();
		if (!trimmed || pending) return;

		const optimisticId = `pending-${crypto.randomUUID()}`;
		setMessages((current) => [
			...current,
			{
				id: optimisticId,
				fromUser: true,
				content: trimmed,
				createdAt: new Date().toISOString(),
			},
		]);
		setInput("");
		setError(null);

		startTransition(async () => {
			try {
				const answer = await askAboutClient(clientId, threadId, trimmed);
				setThreadId(answer.threadId);
				setAxesSeen(answer.axesSeen);
				setMessages((current) => [...current, answer.reply]);
			} catch (err) {
				setError(err instanceof Error ? err.message : "That did not go through.");
				setMessages((current) => current.filter((m) => m.id !== optimisticId));
				setInput(trimmed);
			}
		});
	}

	return (
		<section className="glass-panel space-y-4 p-5">
			<header className="flex flex-wrap items-baseline justify-between gap-2">
				<div>
					<h2 className="text-base font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
						Ask about this client
					</h2>
					<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
						Advisory only. It reads what they shared and cannot change anything.
					</p>
				</div>

				{axesSeen !== null && (
					<span className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
						{axesSeen.length === 0
							? "They have shared nothing for AI use"
							: `Saw: ${axesSeen.map((a) => AXIS_LABELS[a] ?? a).join(", ")}`}
					</span>
				)}
			</header>

			{messages.length > 0 && (
				<ul className="space-y-3">
					{messages.map((m) => (
						<li
							key={m.id}
							className={cn(
								"whitespace-pre-wrap rounded-2xl px-4 py-3 text-sm leading-relaxed",
								m.fromUser
									? "ml-8 bg-verdigris-600 text-white"
									: "mr-8 bg-white text-charcoal-blue-900 ring-1 ring-charcoal-blue-200 dark:bg-charcoal-blue-950/80 dark:text-charcoal-blue-100 dark:ring-white/10",
							)}
						>
							{m.content}
						</li>
					))}
				</ul>
			)}

			{error && (
				<p className="rounded-2xl border border-red-300 bg-red-50 px-4 py-3 text-sm text-red-700 dark:border-red-500/30 dark:bg-red-500/10 dark:text-red-300">
					{error}
				</p>
			)}

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
					disabled={pending}
					placeholder="How has their week been?"
					className="input flex-1 !rounded-2xl !py-2.5 text-sm"
					autoComplete="off"
				/>
				<button
					type="submit"
					disabled={pending || !input.trim()}
					className="btn-primary !rounded-2xl !py-2.5"
					aria-label="Ask"
				>
					<Icon name="arrowRight" size={16} />
				</button>
			</form>
		</section>
	);
}
