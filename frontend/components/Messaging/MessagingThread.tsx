"use client";

import { useCallback, useEffect, useRef, useState, useTransition } from "react";
import { AnimatedIcon } from "@/components/ui/animated-icon";
import { clientApi } from "@/lib/api.client";
import { cn } from "@/lib/utils";

interface Message {
	id: string;
	senderId: string;
	content: string;
	sentAt: string;
}

interface Conversation {
	id: string;
	relationshipId: string;
	messages: Message[];
}

interface MessagingThreadProps {
	relationshipId: string;
	currentUserId: string;
	otherPartyName: string;
	otherPartyRoleLabel: string;
	canMessage: boolean;
}

const POLL_INTERVAL_MS = 15000;

function formatTime(iso: string) {
	try {
		const d = new Date(iso);
		return d.toLocaleTimeString([], { hour: "numeric", minute: "2-digit" });
	} catch {
		return "";
	}
}

function sameDay(a: Date, b: Date) {
	return (
		a.getFullYear() === b.getFullYear() &&
		a.getMonth() === b.getMonth() &&
		a.getDate() === b.getDate()
	);
}

export default function MessagingThread({
	relationshipId,
	currentUserId,
	otherPartyName,
	otherPartyRoleLabel,
	canMessage,
}: MessagingThreadProps) {
	const [conversation, setConversation] = useState<Conversation | null>(null);
	const [input, setInput] = useState("");
	const [loading, setLoading] = useState(true);
	const [error, setError] = useState<string | null>(null);
	const [pending, startTransition] = useTransition();
	const threadRef = useRef<HTMLDivElement>(null);

	const loadConversation = useCallback(async () => {
		try {
			const res = await clientApi<Conversation>(`/api/Chat/${relationshipId}`);
			setConversation(res);
			setError(null);
		} catch (err) {
			const msg = err instanceof Error ? err.message : "Failed to load messages";
			if (!msg.includes("404")) {
				setError(msg);
			}
		} finally {
			setLoading(false);
		}
	}, [relationshipId]);

	useEffect(() => {
		setLoading(true);
		loadConversation();
		const t = setInterval(loadConversation, POLL_INTERVAL_MS);
		return () => clearInterval(t);
	}, [loadConversation]);

	useEffect(() => {
		if (threadRef.current && conversation?.messages.length) {
			threadRef.current.scrollTo({
				top: threadRef.current.scrollHeight,
				behavior: "smooth",
			});
		}
	}, [conversation?.messages.length]);

	async function send() {
		const trimmed = input.trim();
		if (!trimmed || !conversation || pending) return;
		setInput("");
		startTransition(async () => {
			try {
				await clientApi<{ messageId: string }>("/api/Chat/send", {
					method: "POST",
					body: { conversationId: conversation.id, content: trimmed },
				});
				await loadConversation();
			} catch (err) {
				setError(err instanceof Error ? err.message : "Failed to send message");
			}
		});
	}

	function onSubmit(event: React.FormEvent<HTMLFormElement>) {
		event.preventDefault();
		send();
	}

	const messages = conversation?.messages ?? [];

	return (
		<section className="glass-panel flex min-h-[600px] flex-col overflow-hidden p-0">
			<header className="flex items-center gap-3 border-b border-charcoal-blue-200/70 p-4 dark:border-white/10">
				<span className="flex h-11 w-11 items-center justify-center rounded-2xl bg-verdigris-600 text-sm font-semibold text-white">
					{otherPartyName.charAt(0).toUpperCase()}
				</span>
				<div className="flex-1">
					<p className="text-sm font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
						{otherPartyName}
					</p>
					<p className="text-[11px] text-charcoal-blue-500 dark:text-charcoal-blue-400">
						{otherPartyRoleLabel}
					</p>
				</div>
				{!canMessage && (
					<span className="rounded-full border border-burnt-peach-300 px-2 py-0.5 text-[10px] uppercase tracking-[0.14em] text-burnt-peach-700 dark:border-burnt-peach-500/30 dark:text-burnt-peach-300">
						Read-only
					</span>
				)}
			</header>

			<div
				ref={threadRef}
				className="flex-1 space-y-3 overflow-y-auto p-4"
			>
				{loading ? (
					<div className="flex h-full items-center justify-center text-sm text-charcoal-blue-400">
						Loading conversation…
					</div>
				) : messages.length === 0 ? (
					<div className="flex h-full flex-col items-center justify-center gap-3 text-center">
						<span className="icon-chip h-12 w-12 text-charcoal-blue-400">
							<AnimatedIcon name="messageCircle" size={18} />
						</span>
						<p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
							No messages yet. Say hello.
						</p>
					</div>
				) : (
					messages.map((m, i) => {
						const isMine = m.senderId === currentUserId;
						const prev = messages[i - 1];
						const showDateSeparator =
							!prev || !sameDay(new Date(prev.sentAt), new Date(m.sentAt));
						return (
							<div key={m.id}>
								{showDateSeparator && (
									<div className="my-3 flex items-center gap-2 text-[11px] text-charcoal-blue-400">
										<span className="h-px flex-1 bg-charcoal-blue-200 dark:bg-white/10" />
										{new Date(m.sentAt).toLocaleDateString(undefined, {
											weekday: "short",
											month: "short",
											day: "numeric",
										})}
										<span className="h-px flex-1 bg-charcoal-blue-200 dark:bg-white/10" />
									</div>
								)}
								<div
									className={cn(
										"flex gap-2",
										isMine ? "flex-row-reverse" : "flex-row"
									)}
								>
									<div
										className={cn(
											"max-w-[72%] rounded-3xl px-4 py-2.5 text-sm",
											isMine
												? "bg-verdigris-600 text-white shadow-md shadow-verdigris-500/20"
												: "bg-white text-charcoal-blue-900 ring-1 ring-charcoal-blue-200 dark:bg-charcoal-blue-950/80 dark:text-charcoal-blue-100 dark:ring-white/10"
										)}
									>
										<p className="whitespace-pre-wrap">{m.content}</p>
										<p
											className={cn(
												"mt-0.5 text-right text-[10px]",
												isMine ? "text-white/70" : "text-charcoal-blue-400"
											)}
										>
											{formatTime(m.sentAt)}
										</p>
									</div>
								</div>
							</div>
						);
					})
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
					disabled={!canMessage || pending || !conversation}
					placeholder={
						canMessage ? "Type a message…" : "Messaging disabled for this relationship."
					}
					className="input flex-1 !rounded-2xl !py-3"
					autoComplete="off"
				/>
				<button
					type="submit"
					disabled={!canMessage || pending || !conversation || !input.trim()}
					className="btn-primary !rounded-2xl !py-3"
					aria-label="Send"
				>
					<AnimatedIcon name="arrowRight" size={16} />
				</button>
			</form>
		</section>
	);
}
