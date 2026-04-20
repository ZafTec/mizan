"use client";

import { useRef, useState, useTransition } from "react";
import { AnimatedIcon, type AnimatedIconName } from "@/components/ui/animated-icon";
import { clientApi } from "@/lib/api.client";
import { cn } from "@/lib/utils";

interface Message {
	id: string;
	role: "user" | "assistant";
	content: string;
}

interface QuickPrompt {
	id: string;
	label: string;
	prompt: string;
	icon: AnimatedIconName;
}

interface AiChatProps {
	quickPrompts: QuickPrompt[];
}

let messageCounter = 0;
function makeId() {
	messageCounter += 1;
	return `msg-${messageCounter}`;
}

export default function AiChat({ quickPrompts }: AiChatProps) {
	const [messages, setMessages] = useState<Message[]>([]);
	const [input, setInput] = useState("");
	const [pending, startTransition] = useTransition();
	const [error, setError] = useState<string | null>(null);
	const threadRef = useRef<HTMLDivElement>(null);

	function scrollToBottom() {
		requestAnimationFrame(() => {
			threadRef.current?.scrollTo({ top: threadRef.current.scrollHeight, behavior: "smooth" });
		});
	}

	async function send(prompt: string) {
		const trimmed = prompt.trim();
		if (!trimmed || pending) return;

		const userMessage: Message = {
			id: makeId(),
			role: "user",
			content: trimmed,
		};
		setMessages((m) => [...m, userMessage]);
		setInput("");
		setError(null);
		scrollToBottom();

		startTransition(async () => {
			try {
				const res = await clientApi<{ response: string }>("/api/Nutrition/ai/chat", {
					method: "POST",
					body: { message: trimmed },
				});
				const replyText = res?.response?.trim() || "Sorry, I couldn't produce a response.";
				setMessages((m) => [
					...m,
					{ id: makeId(), role: "assistant", content: replyText },
				]);
				scrollToBottom();
			} catch (err) {
				setError(err instanceof Error ? err.message : "Chat request failed.");
			}
		});
	}

	function onSubmit(event: React.FormEvent<HTMLFormElement>) {
		event.preventDefault();
		send(input);
	}

	return (
		<section className="glass-panel flex min-h-[620px] flex-col p-0">
			<header className="flex items-center gap-3 border-b border-charcoal-blue-200/70 p-5 dark:border-white/10">
				<span className="icon-chip h-11 w-11 text-verdigris-700 dark:text-verdigris-300">
					<AnimatedIcon name="bot" size={18} />
				</span>
				<div>
					<h2 className="text-base font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
						Ask the coach
					</h2>
					<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
						Answers use your goal, recent meals, and streak.
					</p>
				</div>
			</header>

			<div ref={threadRef} className="flex-1 space-y-4 overflow-y-auto p-5">
				{messages.length === 0 ? (
					<div className="flex flex-col items-center justify-center gap-4 py-10 text-center">
						<span className="icon-chip h-14 w-14 text-verdigris-700 dark:text-verdigris-300">
							<AnimatedIcon name="sparkles" size={22} />
						</span>
						<p className="max-w-md text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
							Start with a question, or tap a suggested prompt below.
						</p>
						<div className="grid w-full gap-2 sm:grid-cols-2">
							{quickPrompts.map((qp) => (
								<button
									key={qp.id}
									type="button"
									onClick={() => send(qp.prompt)}
									className="group flex items-start gap-3 rounded-2xl border border-charcoal-blue-200 bg-white/80 p-3 text-left text-sm text-charcoal-blue-700 transition-all hover:-translate-y-0.5 hover:border-verdigris-400 hover:shadow-md dark:border-white/10 dark:bg-charcoal-blue-950/60 dark:text-charcoal-blue-200"
								>
									<span className="icon-chip h-8 w-8 text-verdigris-700 dark:text-verdigris-300">
										<AnimatedIcon name={qp.icon} size={14} />
									</span>
									<span className="flex-1">{qp.label}</span>
								</button>
							))}
						</div>
					</div>
				) : (
					messages.map((m) => (
						<div
							key={m.id}
							className={cn(
								"flex gap-3",
								m.role === "user" ? "flex-row-reverse" : "flex-row"
							)}
						>
							<span
								className={cn(
									"flex h-9 w-9 shrink-0 items-center justify-center rounded-2xl",
									m.role === "user"
										? "bg-verdigris-600 text-white"
										: "bg-white text-verdigris-700 ring-1 ring-charcoal-blue-200 dark:bg-charcoal-blue-950 dark:text-verdigris-300 dark:ring-white/10"
								)}
							>
								<AnimatedIcon name={m.role === "user" ? "user" : "bot"} size={14} />
							</span>
							<div
								className={cn(
									"max-w-[78%] whitespace-pre-wrap rounded-3xl px-4 py-3 text-sm leading-relaxed",
									m.role === "user"
										? "bg-verdigris-600 text-white shadow-md shadow-verdigris-500/20"
										: "bg-white text-charcoal-blue-900 shadow-sm ring-1 ring-charcoal-blue-200 dark:bg-charcoal-blue-950/80 dark:text-charcoal-blue-100 dark:ring-white/10"
								)}
							>
								{m.content}
							</div>
						</div>
					))
				)}

				{pending && (
					<div className="flex gap-3">
						<span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-2xl bg-white text-verdigris-700 ring-1 ring-charcoal-blue-200 dark:bg-charcoal-blue-950 dark:text-verdigris-300 dark:ring-white/10">
							<AnimatedIcon name="bot" size={14} />
						</span>
						<div className="flex items-center gap-1.5 rounded-3xl bg-white px-4 py-3 text-sm ring-1 ring-charcoal-blue-200 dark:bg-charcoal-blue-950/80 dark:ring-white/10">
							<span className="h-2 w-2 animate-pulse rounded-full bg-verdigris-500" />
							<span
								className="h-2 w-2 animate-pulse rounded-full bg-verdigris-500"
								style={{ animationDelay: "120ms" }}
							/>
							<span
								className="h-2 w-2 animate-pulse rounded-full bg-verdigris-500"
								style={{ animationDelay: "240ms" }}
							/>
						</div>
					</div>
				)}

				{error && (
					<div className="rounded-2xl border border-red-300 bg-red-50 px-4 py-3 text-sm text-red-700 dark:border-red-500/30 dark:bg-red-500/10 dark:text-red-300">
						{error}
					</div>
				)}
			</div>

			<form
				onSubmit={onSubmit}
				className="flex items-center gap-2 border-t border-charcoal-blue-200/70 p-4 dark:border-white/10"
			>
				<input
					value={input}
					onChange={(e) => setInput(e.target.value)}
					disabled={pending}
					placeholder="Ask the coach…"
					className="input flex-1 !rounded-2xl !py-3"
					autoComplete="off"
				/>
				<button
					type="submit"
					disabled={pending || !input.trim()}
					className="btn-primary !rounded-2xl !py-3"
					aria-label="Send"
				>
					<AnimatedIcon name="arrowRight" size={16} />
				</button>
			</form>
		</section>
	);
}
