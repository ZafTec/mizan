"use client";

import {
  useCallback,
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
  useTransition,
} from "react";
import { clientApi } from "@/lib/api.client";
import { cn } from "@/lib/utils";
import type { components } from "@/types/api.generated";

type Message = components["schemas"]["ChatMessageDto"];
type Conversation = components["schemas"]["ChatConversationDto"];

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
  // Pages fetched with "Show earlier messages". The poll refreshes only the newest page.
  const [older, setOlder] = useState<Message[]>([]);
  const [olderHasMore, setOlderHasMore] = useState<boolean | null>(null);
  const [loadingOlder, setLoadingOlder] = useState(false);
  const threadRef = useRef<HTMLDivElement>(null);
  const heightBeforeOlder = useRef<number | null>(null);

  const loadConversation = useCallback(async () => {
    try {
      const res = await clientApi<Conversation>(`/api/Chat/${relationshipId}`);
      setConversation(res);
      setError(null);
    } catch (err) {
      const msg =
        err instanceof Error ? err.message : "Failed to load messages";
      if (!msg.includes("404")) {
        setError(msg);
      }
    } finally {
      setLoading(false);
    }
  }, [relationshipId]);

  useEffect(() => {
    setLoading(true);
    setOlder([]);
    setOlderHasMore(null);
    loadConversation();
    const t = setInterval(loadConversation, POLL_INTERVAL_MS);
    return () => clearInterval(t);
  }, [loadConversation]);

  const latest = conversation?.messages ?? [];
  const newestId = latest.at(-1)?.id;
  useEffect(() => {
    if (threadRef.current && newestId) {
      threadRef.current.scrollTo({
        top: threadRef.current.scrollHeight,
        behavior: "smooth",
      });
    }
  }, [newestId]);

  // Earlier messages go above what the person is reading, so it stays in place.
  useLayoutEffect(() => {
    const thread = threadRef.current;
    if (thread && heightBeforeOlder.current !== null) {
      thread.scrollTop += thread.scrollHeight - heightBeforeOlder.current;
      heightBeforeOlder.current = null;
    }
  }, [older]);

  const seen = new Set(older.map((m) => m.id));
  const messages = [...older, ...latest.filter((m) => !seen.has(m.id))];
  const hasOlder = olderHasMore ?? conversation?.hasOlder ?? false;

  async function loadOlder() {
    const first = messages[0];
    if (!first || loadingOlder) return;
    setLoadingOlder(true);
    try {
      const page = await clientApi<Conversation>(
        `/api/Chat/${relationshipId}?before=${encodeURIComponent(first.id)}`,
      );
      heightBeforeOlder.current = threadRef.current?.scrollHeight ?? null;
      setOlder((current) => [...page.messages, ...current]);
      setOlderHasMore(page.hasOlder);
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Failed to load earlier messages",
      );
    } finally {
      setLoadingOlder(false);
    }
  }

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

  return (
    <section className="card overflow-visible flex min-h-[600px] flex-col overflow-hidden p-0">
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

      <div ref={threadRef} className="flex-1 space-y-3 overflow-y-auto p-4">
        {loading ? (
          <div className="flex h-full items-center justify-center text-sm text-charcoal-blue-400">
            Loading conversation…
          </div>
        ) : messages.length === 0 ? (
          <div className="flex h-full flex-col items-center justify-center gap-3 text-center">
            <span className="inline-flex items-center justify-center text-charcoal-blue-700 dark:text-charcoal-blue-300 h-12 w-12"></span>
            <p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
              No messages yet. Say hello.
            </p>
          </div>
        ) : (
          <>
            {hasOlder && (
              <div className="flex justify-center">
                <button
                  type="button"
                  className="btn-ghost btn-sm min-h-9"
                  disabled={loadingOlder}
                  onClick={loadOlder}
                >
                  {loadingOlder ? "Loading…" : "Show earlier messages"}
                </button>
              </div>
            )}
            {messages.map((m, i) => {
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
                      isMine ? "flex-row-reverse" : "flex-row",
                    )}
                  >
                    <div
                      className={cn(
                        "max-w-[72%] rounded-3xl px-4 py-2.5 text-sm",
                        isMine
                          ? "bg-verdigris-600 text-white "
                          : "bg-white text-charcoal-blue-900 ring-1 ring-charcoal-blue-200 dark:bg-charcoal-blue-950/80 dark:text-charcoal-blue-100 dark:ring-white/10",
                      )}
                    >
                      <p className="whitespace-pre-wrap">{m.content}</p>
                      <p
                        className={cn(
                          "mt-0.5 text-right text-[10px]",
                          isMine ? "text-white/70" : "text-charcoal-blue-400",
                        )}
                      >
                        {formatTime(m.sentAt)}
                      </p>
                    </div>
                  </div>
                </div>
              );
            })}
          </>
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
            canMessage
              ? "Type a message…"
              : "Messaging disabled for this relationship."
          }
          className="input flex-1 !rounded-2xl !py-3"
          autoComplete="off"
        />
        <button
          type="submit"
          disabled={!canMessage || pending || !conversation || !input.trim()}
          className="btn-primary !rounded-2xl !py-3"
          aria-label="Send"
        ></button>
      </form>
    </section>
  );
}
