"use client";

import { useEffect, useRef, useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import { Icon, type IconName } from "@/components/ui/icon";
import AiMarkdown from "@/components/ai/AiMarkdown";
import {
	deleteAiChatThread,
	getAiChatThread,
	listAiChatThreads,
	sendAiChatImage,
	sendAiChatMessage,
	type AiChatMessage,
	type AiChatThread,
	type AiToolInvocation,
} from "@/lib/api/ai";
import { cn } from "@/lib/utils";

interface QuickPrompt {
	id: string;
	label: string;
	prompt: string;
	icon: IconName;
}

interface AiChatProps {
	quickPrompts: QuickPrompt[];
}

export default function AiChat({ quickPrompts }: AiChatProps) {
	const router = useRouter();
	const [threads, setThreads] = useState<AiChatThread[]>([]);
	const [threadId, setThreadId] = useState<string | null>(null);
	const [messages, setMessages] = useState<AiChatMessage[]>([]);
	// What each reply did, keyed by message id. Only for the current session:
	// tool invocations are echoes of a turn, not part of the transcript.
	const [performed, setPerformed] = useState<Record<string, AiToolInvocation[]>>({});
	const [attachment, setAttachment] = useState<File | null>(null);
	const fileRef = useRef<HTMLInputElement>(null);
	const [input, setInput] = useState("");
	const [pending, startTransition] = useTransition();
	const [error, setError] = useState<string | null>(null);
	const threadRef = useRef<HTMLDivElement>(null);

	useEffect(() => {
		// Past conversations, so the screen opens on something rather than
		// pretending nothing was ever said.
		listAiChatThreads()
			.then(setThreads)
			.catch(() => setThreads([]));
	}, []);

	function scrollToBottom() {
		requestAnimationFrame(() => {
			threadRef.current?.scrollTo({
				top: threadRef.current.scrollHeight,
				behavior: "smooth",
			});
		});
	}

	function openThread(id: string) {
		setError(null);
		startTransition(async () => {
			try {
				const thread = await getAiChatThread(id);
				setThreadId(thread.id);
				setMessages(thread.messages);
				scrollToBottom();
			} catch (err) {
				setError(err instanceof Error ? err.message : "Could not open that conversation.");
			}
		});
	}

	function startNew() {
		setThreadId(null);
		setMessages([]);
		setError(null);
	}

	function remove(id: string) {
		startTransition(async () => {
			try {
				await deleteAiChatThread(id);
				setThreads((t) => t.filter((thread) => thread.id !== id));
				if (threadId === id) startNew();
			} catch (err) {
				setError(err instanceof Error ? err.message : "Could not delete that conversation.");
			}
		});
	}

	async function send(prompt: string) {
		const trimmed = prompt.trim();
		const file = attachment;
		// A photo on its own is a turn; text on its own is a turn; nothing is not.
		if ((!trimmed && !file) || pending) return;

		const optimisticId = `pending-${crypto.randomUUID()}`;
		setMessages((m) => [
			...m,
			{
				id: optimisticId,
				fromUser: true,
				content: trimmed,
				createdAt: new Date().toISOString(),
				// Shown from the local file until the server comes back with the
				// stored URL, so the photo appears the moment it is sent.
				imageUrl: file ? URL.createObjectURL(file) : null,
			},
		]);
		setInput("");
		setAttachment(null);
		setError(null);
		scrollToBottom();

		startTransition(async () => {
			try {
				const turn = file
					? await sendAiChatImage(threadId, trimmed, file)
					: await sendAiChatMessage(threadId, trimmed);
				setThreadId(turn.threadId);
				setMessages((m) => [...m, turn.reply]);
				if (turn.performed?.length) {
					setPerformed((current) => ({ ...current, [turn.reply.id]: turn.performed }));
					// Something was written, so anything on screen behind this
					// is stale.
					router.refresh();
				}
				setThreads((current) => {
					const rest = current.filter((t) => t.id !== turn.threadId);
					return [
						{ id: turn.threadId, title: turn.title, updatedAt: turn.reply.createdAt },
						...rest,
					];
				});
				scrollToBottom();
			} catch (err) {
				// 429 already says which ceiling tripped and when it resets, and
				// 503 says the assistant is unavailable. Both are more useful
				// than anything this component could invent.
				setError(err instanceof Error ? err.message : "Chat request failed.");
				// The turn was never recorded, so the screen must not keep it.
				setMessages((m) => m.filter((message) => message.id !== optimisticId));
				setInput(trimmed);
				setAttachment(file);
			}
		});
	}

	function onSubmit(event: React.FormEvent<HTMLFormElement>) {
		event.preventDefault();
		send(input);
	}

	return (
		<div className="grid gap-4 lg:grid-cols-[220px_minmax(0,1fr)]">
			<aside className="space-y-2">
				<button type="button" onClick={startNew} className="btn-secondary w-full !rounded-2xl">
					New conversation
				</button>

				{threads.length > 0 && (
					<ul className="space-y-1">
						{threads.map((thread) => (
							<li key={thread.id} className="group flex items-center gap-1">
								<button
									type="button"
									onClick={() => openThread(thread.id)}
									className={cn(
										"flex-1 truncate rounded-xl px-3 py-2 text-left text-sm transition-colors",
										thread.id === threadId
											? "bg-verdigris-50 text-verdigris-900 dark:bg-verdigris-500/10 dark:text-verdigris-200"
											: "text-charcoal-blue-600 hover:bg-charcoal-blue-100 dark:text-charcoal-blue-300 dark:hover:bg-white/5",
									)}
								>
									{thread.title}
								</button>
								<button
									type="button"
									onClick={() => remove(thread.id)}
									aria-label={`Delete ${thread.title}`}
									title="Delete conversation"
									className="rounded-lg p-1.5 text-charcoal-blue-400 opacity-0 transition-opacity hover:text-red-600 focus-visible:opacity-100 group-hover:opacity-100"
								>
									<Icon name="x" size={14} />
								</button>
							</li>
						))}
					</ul>
				)}
			</aside>

			{/*
			  * Bounded height, not min-height: the message list below is the
			  * scroller, and it can only scroll if something above it stops
			  * growing. With min-h alone the panel stretched to fit every
			  * message and the whole page scrolled instead.
			  */}
			<section className="glass-panel flex h-[70dvh] min-h-[480px] flex-col p-0">
				<header className="flex items-center gap-3 border-b border-charcoal-blue-200/70 p-5 dark:border-white/10">
					<span className="icon-chip h-11 w-11 text-verdigris-700 dark:text-verdigris-300">
						<Icon name="bot" size={18} />
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
								<Icon name="sparkles" size={22} />
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
										className="group flex items-start gap-3 rounded-2xl border border-charcoal-blue-200 bg-charcoal-blue-50 p-3 text-left text-sm text-charcoal-blue-700 transition-all hover:-translate-y-0.5 hover:border-verdigris-400 dark:border-white/10 dark:bg-charcoal-blue-950 dark:text-charcoal-blue-200"
									>
										<span className="icon-chip h-8 w-8 text-verdigris-700 dark:text-verdigris-300">
											<Icon name={qp.icon} size={14} />
										</span>
										<span className="flex-1">{qp.label}</span>
									</button>
								))}
							</div>
						</div>
					) : (
						messages.map((m) => (
							<div key={m.id} className="space-y-2">
							<div
								className={cn("flex gap-3", m.fromUser ? "flex-row-reverse" : "flex-row")}
							>
								<span
									className={cn(
										"flex h-9 w-9 shrink-0 items-center justify-center rounded-2xl",
										m.fromUser
											? "bg-verdigris-600 text-white"
											: "bg-white text-verdigris-700 ring-1 ring-charcoal-blue-200 dark:bg-charcoal-blue-950 dark:text-verdigris-300 dark:ring-white/10",
									)}
								>
									<Icon name={m.fromUser ? "user" : "bot"} size={14} />
								</span>
								<div
									className={cn(
										"max-w-[78%] rounded-3xl px-4 py-3 text-sm leading-relaxed",
										m.fromUser
											? "whitespace-pre-wrap bg-verdigris-600 text-white "
											: "bg-white text-charcoal-blue-900 ring-1 ring-charcoal-blue-200 dark:bg-charcoal-blue-950/80 dark:text-charcoal-blue-100 dark:ring-white/10",
									)}
								>
									{m.imageUrl && (
										/* eslint-disable-next-line @next/next/no-img-element --
										   the object store is configured per deployment, so it is
										   not in next/image's host allowlist. */
										<img
											src={m.imageUrl}
											alt="Attached"
											className="mb-2 max-h-56 w-auto rounded-2xl"
										/>
									)}
									{/* Only the assistant writes markdown. A user's asterisks
									    are asterisks. */}
									{m.fromUser ? m.content : <AiMarkdown content={m.content} />}
								</div>
								</div>

								{performed[m.id]?.length > 0 && (
									<ul className="ml-12 space-y-1">
										{performed[m.id].map((action, i) => (
											<li
												key={`${action.tool}-${i}`}
												className={cn(
													"flex items-start gap-2 text-xs",
													action.succeeded
														? "text-verdigris-700 dark:text-verdigris-300"
														: "text-amber-700 dark:text-amber-300",
												)}
											>
												<Icon
													name={action.succeeded ? "circleCheck" : "badgeAlert"}
													size={13}
													className="mt-0.5 shrink-0"
												/>
												<span>{action.succeeded ? action.summary : action.error}</span>
											</li>
										))}
									</ul>
								)}
							</div>
						))
					)}

					{pending && (
						<div className="flex gap-3">
							<span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-2xl bg-white text-verdigris-700 ring-1 ring-charcoal-blue-200 dark:bg-charcoal-blue-950 dark:text-verdigris-300 dark:ring-white/10">
								<Icon name="bot" size={14} />
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
					className="space-y-2 border-t border-charcoal-blue-200/70 p-4 dark:border-white/10"
				>
					{attachment && (
						<div className="flex items-center gap-2 rounded-2xl bg-charcoal-blue-100 px-3 py-2 text-xs dark:bg-white/5">
							<Icon name="sparkles" size={14} className="shrink-0 text-verdigris-600" />
							<span className="min-w-0 flex-1 truncate text-charcoal-blue-600 dark:text-charcoal-blue-300">
								{attachment.name}
							</span>
							<button
								type="button"
								onClick={() => setAttachment(null)}
								aria-label="Remove photo"
								className="shrink-0 rounded-lg p-1 text-charcoal-blue-400 transition-colors hover:text-red-600"
							>
								<Icon name="x" size={13} />
							</button>
						</div>
					)}

					<div className="flex items-center gap-2">
						<input
							ref={fileRef}
							type="file"
							accept="image/jpeg,image/png,image/webp"
							className="hidden"
							onChange={(e) => {
								const picked = e.target.files?.[0] ?? null;
								e.target.value = "";
								setAttachment(picked);
							}}
						/>
						<button
							type="button"
							onClick={() => fileRef.current?.click()}
							disabled={pending}
							aria-label="Attach a photo"
							title="Attach a photo"
							className="btn-secondary !rounded-2xl !px-3 !py-3 disabled:opacity-60"
						>
							<Icon name="cookingPot" size={16} />
						</button>
						<input
							value={input}
							onChange={(e) => setInput(e.target.value)}
							disabled={pending}
							placeholder={attachment ? "Say something about it (optional)…" : "Ask the coach…"}
							className="input flex-1 !rounded-2xl !py-3"
							autoComplete="off"
						/>
						<button
							type="submit"
							disabled={pending || (!input.trim() && !attachment)}
							className="btn-primary !rounded-2xl !py-3"
							aria-label="Send"
						>
							<Icon name="arrowRight" size={16} />
						</button>
					</div>
				</form>
			</section>
		</div>
	);
}
