"use client";

import Image from "next/image";
import {
  useCallback,
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
  useTransition,
  type FormEvent,
  type KeyboardEvent,
  type MouseEvent,
  type ReactNode,
} from "react";
import { useRouter } from "next/navigation";
import * as Dialog from "@radix-ui/react-dialog";
import {
  ArrowLeft,
  ArrowUp,
  Check,
  ChevronDown,
  CircleAlert,
  Copy,
  History,
  Plus,
  SquarePen,
  X,
} from "lucide-react";
import AiMarkdown from "@/components/ai/AiMarkdown";
import AiCardList from "@/components/ai/cards/AiCardView";
import AiPermissionGate, {
  consentMissing,
  describeRequirement,
  grantRequirement,
} from "@/components/ai/AiPermissionGate";
import ConfirmationModal from "@/components/ConfirmationModal";
import {
  deleteAiChatThread,
  getAiChatThread,
  getAiConsent,
  listAiChatThreads,
  sendAiChatImage,
  sendAiChatMessage,
  updateAiConsent,
  type AiCard,
  type AiChatMessage,
  type AiChatThread,
  type AiConsent,
  type AiConsentRequirement,
  type AiToolInvocation,
} from "@/lib/api/ai";
import { getErrorMessage } from "@/lib/toast";
import { cn } from "@/lib/utils";

const QUICK_PROMPTS = [
  {
    id: "fit-remaining",
    label: "Ideas for my remaining macros today",
    prompt:
      "What are 3 quick meal ideas that fit my remaining macros for today?",
  },
  {
    id: "weekly-review",
    label: "Review my last 7 days",
    prompt:
      "Summarise my nutrition and training over the past 7 days and flag any trends.",
  },
  {
    id: "protein-gap",
    label: "High-protein snack ideas",
    prompt:
      "Give me 5 high-protein snacks under 250 calories I can keep on hand.",
  },
  {
    id: "workout-plan",
    label: "What to focus on in my next workout",
    prompt:
      "Based on my recent workouts, what should I focus on in my next session?",
  },
];

/** Said once the person chose Not now, so the assistant's silence about their log has a reason. */
export const NO_ACCESS_LINE =
  "I can't see your log yet. Allow access to get answers about your data.";

const MAX_ROWS = 6;
const NOT_NOW_KEY = "mizan.assistant.permission-later";

function timeLabel(iso: string) {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return "";
  return date.toLocaleTimeString([], { hour: "numeric", minute: "2-digit" });
}

function sameDay(iso: string, now: Date) {
  const date = new Date(iso);
  return (
    date.getFullYear() === now.getFullYear() &&
    date.getMonth() === now.getMonth() &&
    date.getDate() === now.getDate()
  );
}

function readNotNow() {
  try {
    return window.localStorage.getItem(NOT_NOW_KEY) === "1";
  } catch {
    return false;
  }
}

function writeNotNow(value: boolean) {
  try {
    if (value) window.localStorage.setItem(NOT_NOW_KEY, "1");
    else window.localStorage.removeItem(NOT_NOW_KEY);
  } catch {
    // Without storage the step simply asks again next time.
  }
}

/** Whether the consent the person holds now already allows what a refusal asked for. */
function allows(consent: AiConsent | null, requirement: AiConsentRequirement) {
  if (!consent) return false;
  const axis = requirement.axis;
  if (requirement.access === "write")
    return (
      consent.allowWrites &&
      (axis === "training"
        ? consent.writeTraining
        : axis === "body"
          ? consent.writeBody
          : consent.writeNutrition)
    );
  return (
    consent.enabled &&
    (axis === "training"
      ? consent.shareTraining
      : axis === "body"
        ? consent.shareBody
        : consent.shareNutrition)
  );
}

/**
 * The assistant's conversation, as the shell's panel holds it (components/ai/Assistant.tsx). The
 * header switches between the transcript and the list of past conversations. Until the person has
 * chosen what it may use, the permission step stands where the suggestions would. A reply whose
 * tool call was refused for consent carries one button that turns on just that switch, then offers
 * to send the question again.
 */
export default function AiChat({
  threadId,
  onThreadChange,
  onClose,
  phone = false,
  focusToken = 0,
}: {
  threadId: string | null;
  onThreadChange: (id: string | null) => void;
  onClose: () => void;
  phone?: boolean;
  /** Changes each time the panel opens, asking for focus once there is something to focus. */
  focusToken?: number;
}) {
  const router = useRouter();
  const [view, setView] = useState<"chat" | "history">("chat");
  const [threads, setThreads] = useState<AiChatThread[]>([]);
  const [messages, setMessages] = useState<AiChatMessage[]>([]);
  // What each reply did, keyed by message id. Only for this visit: tool invocations are echoes of a turn, not part
  // of the transcript.
  const [performed, setPerformed] = useState<
    Record<string, AiToolInvocation[]>
  >({});
  // The typed cards each reply carried (a plan, a day, a cost table, a list), keyed the same way and for the
  // same reason: they come with the turn, not with the stored transcript.
  const [cards, setCards] = useState<Record<string, AiCard[]>>({});
  // The switches a reply's tool calls were refused for, keyed the same way.
  const [refusals, setRefusals] = useState<
    Record<string, AiConsentRequirement[]>
  >({});
  const [consent, setConsent] = useState<AiConsent | null>(null);
  const [consentChecked, setConsentChecked] = useState(false);
  const [notNow, setNotNow] = useState(false);
  const [granting, setGranting] = useState<string | null>(null);
  const [attachment, setAttachment] = useState<{
    file: File;
    url: string;
  } | null>(null);
  const [input, setInput] = useState("");
  const [pending, startTransition] = useTransition();
  const [error, setError] = useState<string | null>(null);
  const [lightbox, setLightbox] = useState<{
    src: string;
    label: string;
  } | null>(null);
  const [deleting, setDeleting] = useState<AiChatThread | null>(null);
  const [search, setSearch] = useState("");
  const transcript = useRef<HTMLDivElement>(null);
  const textarea = useRef<HTMLTextAreaElement>(null);
  const fileInput = useRef<HTMLInputElement>(null);
  const historyButton = useRef<HTMLButtonElement>(null);
  const stickToBottom = useRef(true);
  // The conversation on screen. A thread the shell names that is not this one is loaded.
  const loaded = useRef<string | null>(null);

  useEffect(() => {
    listAiChatThreads()
      .then(setThreads)
      .catch(() => setThreads([]));
    getAiConsent()
      .then(setConsent)
      .catch(() => setConsent(null))
      .finally(() => setConsentChecked(true));
    setNotNow(readNotNow());
  }, []);

  // A thread named from outside (a reload, a `/ai?thread=` link) opens here. It counts as loaded
  // only once its messages arrive, so an interrupted load is tried again.
  useEffect(() => {
    if (threadId === loaded.current) return;
    if (!threadId) {
      loaded.current = null;
      setMessages([]);
      return;
    }
    let current = true;
    getAiChatThread(threadId)
      .then((thread) => {
        if (!current) return;
        loaded.current = thread.id;
        setMessages(thread.messages);
        stickToBottom.current = true;
      })
      .catch(() => {
        // Deleted elsewhere, or never this person's: start fresh rather than show an error.
        if (current) onThreadChange(null);
      });
    return () => {
      current = false;
    };
  }, [threadId, onThreadChange]);

  const root = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!focusToken || !consentChecked) return;
    const frame = requestAnimationFrame(() => {
      const panel = root.current;
      panel
        ?.querySelector<HTMLElement>(
          "#ai-message:not([disabled]), [data-autofocus]",
        )
        ?.focus();
    });
    return () => cancelAnimationFrame(frame);
  }, [focusToken, consentChecked]);

  // A thumbnail's address is a handle on memory. It is released when the photo is removed, when the server's
  // stored address replaces it in the transcript, or when the screen goes - never while something still shows it.
  const previews = useRef(new Set<string>());
  const preview = useCallback((file: File) => {
    const url = URL.createObjectURL(file);
    previews.current.add(url);
    return url;
  }, []);
  const release = useCallback((url: string) => {
    if (!previews.current.delete(url)) return;
    URL.revokeObjectURL(url);
  }, []);
  useEffect(
    () => () => {
      for (const url of previews.current) URL.revokeObjectURL(url);
      previews.current.clear();
    },
    [],
  );

  const scrollToBottom = useCallback(() => {
    requestAnimationFrame(() => {
      const el = transcript.current;
      if (el && stickToBottom.current) el.scrollTo({ top: el.scrollHeight });
    });
  }, []);

  useLayoutEffect(() => {
    // The empty state reads top-down; only a transcript follows its newest line.
    if (messages.length > 0 || pending) scrollToBottom();
  }, [messages, pending, scrollToBottom]);

  // The box grows with what is typed, up to a few lines, then scrolls.
  useLayoutEffect(() => {
    const el = textarea.current;
    if (!el) return;
    el.style.height = "auto";
    const line = parseFloat(getComputedStyle(el).lineHeight) || 20;
    el.style.height = `${Math.min(el.scrollHeight, line * MAX_ROWS + 24)}px`;
  }, [input]);

  // The composer takes focus back once it can: it is disabled while a turn or a thread loads.
  const [focusRequest, setFocusRequest] = useState(0);
  const focusServed = useRef(0);
  useEffect(() => {
    if (focusServed.current === focusRequest || pending || view !== "chat")
      return;
    focusServed.current = focusRequest;
    requestAnimationFrame(() => textarea.current?.focus());
  }, [focusRequest, pending, view]);

  function showChat() {
    setView("chat");
    setFocusRequest((count) => count + 1);
  }

  function openThread(id: string) {
    setError(null);
    startTransition(async () => {
      try {
        const thread = await getAiChatThread(id);
        loaded.current = thread.id;
        onThreadChange(thread.id);
        setMessages(thread.messages);
        stickToBottom.current = true;
        showChat();
      } catch (cause) {
        setError(getErrorMessage(cause, "Could not open that conversation."));
      }
    });
  }

  function startNew() {
    loaded.current = null;
    onThreadChange(null);
    setMessages([]);
    setError(null);
    showChat();
  }

  function confirmDelete() {
    const thread = deleting;
    if (!thread) return;
    setDeleting(null);
    startTransition(async () => {
      try {
        await deleteAiChatThread(thread.id);
        setThreads((current) => current.filter((t) => t.id !== thread.id));
        if (threadId === thread.id) {
          loaded.current = null;
          onThreadChange(null);
          setMessages([]);
        }
      } catch (cause) {
        setError(getErrorMessage(cause, "Could not delete that conversation."));
      }
    });
  }

  function attach(file: File | null) {
    if (attachment) release(attachment.url);
    setAttachment(file ? { file, url: preview(file) } : null);
  }

  function chooseNotNow() {
    writeNotNow(true);
    setNotNow(true);
    showChat();
  }

  function granted(next: AiConsent) {
    setConsent(next);
    writeNotNow(false);
    setNotNow(false);
    showChat();
  }

  async function allow(key: string, requirement: AiConsentRequirement) {
    setGranting(key);
    setError(null);
    try {
      const current = consent ?? (await getAiConsent());
      setConsent(await updateAiConsent(grantRequirement(current, requirement)));
    } catch (cause) {
      setError(getErrorMessage(cause, "Could not change the permission."));
    } finally {
      setGranting(null);
    }
  }

  async function send(prompt: string) {
    const text = prompt.trim();
    const photo = attachment;
    // A photo on its own is a turn; text on its own is a turn; nothing is not.
    if ((!text && !photo) || pending) return;

    const optimisticId = `pending-${crypto.randomUUID()}`;
    stickToBottom.current = true;
    setMessages((current) => [
      ...current,
      {
        id: optimisticId,
        fromUser: true,
        content: text,
        createdAt: new Date().toISOString(),
        // Shown from the local file until the server answers with the stored address, so the photo appears
        // the moment it is sent.
        imageUrl: photo?.url ?? null,
      },
    ]);
    setInput("");
    setAttachment(null);
    setError(null);

    startTransition(async () => {
      try {
        const turn = photo
          ? await sendAiChatImage(threadId, text, photo.file)
          : await sendAiChatMessage(threadId, text);
        loaded.current = turn.threadId;
        onThreadChange(turn.threadId);
        // The stored message takes the optimistic one's place, so the photo now comes from its stored address
        // and the local preview can go.
        setMessages((current) => [
          ...current.map((m) => (m.id === optimisticId ? turn.message : m)),
          turn.reply,
        ]);
        if (photo) release(photo.url);
        if (turn.cards?.length)
          setCards((current) => ({ ...current, [turn.reply.id]: turn.cards }));
        if (turn.consentRequired?.length)
          setRefusals((current) => ({
            ...current,
            [turn.reply.id]: turn.consentRequired,
          }));
        if (turn.performed?.some((action) => action.succeeded))
          // Something was written, so anything on screen behind this is stale.
          router.refresh();
        if (turn.performed?.length)
          setPerformed((current) => ({
            ...current,
            [turn.reply.id]: turn.performed,
          }));
        setThreads((current) => [
          {
            id: turn.threadId,
            title: turn.title,
            updatedAt: turn.reply.createdAt,
          },
          ...current.filter((t) => t.id !== turn.threadId),
        ]);
      } catch (cause) {
        // 429 already says which ceiling tripped and when it resets, and 503 says the assistant is
        // unavailable. Both say more than this component could.
        setError(getErrorMessage(cause, "Chat request failed."));
        // The turn was never recorded, so the screen must not keep it, and the person gets their words back.
        setMessages((current) => current.filter((m) => m.id !== optimisticId));
        setInput(text);
        // The preview was never released, so the same photo returns to the composer as it was.
        if (photo) setAttachment(photo);
      }
    });
  }

  /** The question a reply answered, so a refused one can be asked again once allowed. */
  function questionBefore(replyId: string) {
    const index = messages.findIndex((m) => m.id === replyId);
    for (let i = index - 1; i >= 0; i--)
      if (messages[i].fromUser) return messages[i].content;
    return "";
  }

  function onSubmit(event: FormEvent) {
    event.preventDefault();
    void send(input);
  }

  function onKeyDown(event: KeyboardEvent<HTMLTextAreaElement>) {
    // Enter sends, Shift+Enter starts a new line. Not while an input method is still composing a word.
    if (
      event.key === "Enter" &&
      !event.shiftKey &&
      !event.nativeEvent.isComposing
    ) {
      event.preventDefault();
      void send(input);
    }
  }

  const missing = consentMissing(consent);
  const asking = missing && !notNow;
  const title = threads.find((t) => t.id === threadId)?.title;

  return (
    <div ref={root} className="flex h-full min-h-0 flex-col">
      {phone && (
        <div aria-hidden="true" className="flex justify-center pt-2">
          <span className="h-1 w-9 rounded-full bg-border" />
        </div>
      )}
      <header className="flex items-center gap-1 border-b border-border py-2 pl-4 pr-2">
        {view === "history" ? (
          <>
            <button
              type="button"
              onClick={showChat}
              aria-label="Back to the conversation"
              title="Back to the conversation"
              className="-ml-2 flex h-11 w-11 shrink-0 items-center justify-center rounded-sm hover:bg-muted"
            >
              <ArrowLeft aria-hidden="true" size={19} strokeWidth={1.7} />
            </button>
            <h2
              id="assistant-title"
              className="min-w-0 flex-1 truncate text-base font-semibold"
            >
              Conversations
            </h2>
          </>
        ) : (
          <div className="min-w-0 flex-1">
            <h2
              id="assistant-title"
              className="text-base font-semibold leading-tight"
            >
              Mizan
            </h2>
            <p className="truncate text-xs text-muted-foreground">
              {title ?? "New conversation"}
            </p>
          </div>
        )}
        {view === "chat" && (
          <button
            ref={historyButton}
            type="button"
            onClick={() => setView("history")}
            aria-pressed={false}
            aria-label="Past conversations"
            title="Past conversations"
            className="flex h-11 w-11 shrink-0 items-center justify-center rounded-sm hover:bg-muted"
          >
            <History aria-hidden="true" size={19} strokeWidth={1.7} />
          </button>
        )}
        <button
          type="button"
          onClick={startNew}
          aria-label="New conversation"
          title="New conversation"
          className="flex h-11 w-11 shrink-0 items-center justify-center rounded-sm hover:bg-muted"
        >
          <SquarePen aria-hidden="true" size={18} strokeWidth={1.7} />
        </button>
        <button
          type="button"
          onClick={onClose}
          aria-label="Close assistant"
          title="Close assistant"
          className="flex h-11 w-11 shrink-0 items-center justify-center rounded-sm hover:bg-muted"
        >
          {phone ? (
            <ChevronDown aria-hidden="true" size={20} strokeWidth={1.7} />
          ) : (
            <X aria-hidden="true" size={19} strokeWidth={1.7} />
          )}
        </button>
      </header>

      {!consentChecked ? (
        <div className="flex-1" />
      ) : view === "history" ? (
        <ConversationList
          threads={threads}
          activeId={threadId}
          search={search}
          onSearch={setSearch}
          onOpen={openThread}
          onDelete={setDeleting}
        />
      ) : asking && consent ? (
        <AiPermissionGate
          consent={consent}
          onGranted={granted}
          onNotNow={chooseNotNow}
        />
      ) : (
        <>
          <div
            ref={transcript}
            role="log"
            aria-label="Conversation"
            aria-live="polite"
            onScroll={(event) => {
              const el = event.currentTarget;
              stickToBottom.current =
                el.scrollHeight - el.scrollTop - el.clientHeight < 80;
            }}
            className="min-h-0 flex-1 space-y-5 overflow-y-auto overscroll-contain px-4 py-5"
          >
            {missing && (
              <div className="flex flex-wrap items-center gap-x-3 gap-y-1 border-b border-border pb-4 text-sm">
                <p className="text-muted-foreground">{NO_ACCESS_LINE}</p>
                <button
                  type="button"
                  onClick={() => {
                    writeNotNow(false);
                    setNotNow(false);
                  }}
                  className="text-link underline underline-offset-4"
                >
                  Allow access
                </button>
              </div>
            )}

            {messages.length === 0 ? (
              <EmptyState
                onPick={(prompt) => void send(prompt)}
                disabled={pending}
              />
            ) : (
              messages.map((message) => (
                <MessageView
                  key={message.id}
                  message={message}
                  actions={performed[message.id]}
                  cards={cards[message.id]}
                  onOpenImage={(src) =>
                    setLightbox({
                      src,
                      label: message.fromUser
                        ? "Photo you sent"
                        : "Photo from the assistant",
                    })
                  }
                >
                  {refusals[message.id]?.map((requirement) => {
                    const key = `${message.id}-${requirement.axis}-${requirement.access}`;
                    return (
                      <PermissionNeeded
                        key={key}
                        requirement={requirement}
                        allowed={allows(consent, requirement)}
                        busy={granting === key || pending}
                        onAllow={() => void allow(key, requirement)}
                        onSendAgain={() =>
                          void send(questionBefore(message.id))
                        }
                      />
                    );
                  })}
                </MessageView>
              ))
            )}

            {pending && (
              <div
                role="status"
                className="flex items-center gap-2 text-sm text-muted-foreground transition-opacity duration-200 ease-out starting:opacity-0"
              >
                <span className="flex gap-1" aria-hidden="true">
                  {[0, 1, 2].map((dot) => (
                    <span
                      key={dot}
                      className="h-1.5 w-1.5 animate-pulse rounded-full bg-brand-600 motion-reduce:animate-none"
                      style={{ animationDelay: `${dot * 120}ms` }}
                    />
                  ))}
                </span>
                The assistant is writing
              </div>
            )}

            {error && (
              <p
                role="alert"
                className="text-sm text-burnt-peach-700 dark:text-burnt-peach-300"
              >
                {error}
              </p>
            )}
          </div>

          <form
            onSubmit={onSubmit}
            className="space-y-2 border-t border-border p-3 pb-[calc(0.75rem+env(safe-area-inset-bottom,0px))]"
          >
            {attachment && (
              <div className="flex items-center gap-3">
                <button
                  type="button"
                  onClick={() =>
                    setLightbox({ src: attachment.url, label: "Photo to send" })
                  }
                  aria-label="Preview the photo to send"
                  className="relative h-14 w-14 shrink-0 overflow-hidden rounded-sm border border-border"
                >
                  <PictureImage
                    src={attachment.url}
                    alt=""
                    className="object-cover"
                  />
                </button>
                <span className="min-w-0 flex-1 truncate text-sm text-muted-foreground">
                  {attachment.file.name}
                </span>
                <button
                  type="button"
                  onClick={() => attach(null)}
                  aria-label="Remove photo"
                  className="flex h-11 w-11 shrink-0 items-center justify-center rounded-sm hover:bg-muted"
                >
                  <X aria-hidden="true" size={18} strokeWidth={1.7} />
                </button>
              </div>
            )}

            <div className="rounded-md border border-border bg-background transition-colors focus-within:border-foreground">
              <input
                ref={fileInput}
                type="file"
                accept="image/jpeg,image/png,image/webp"
                className="hidden"
                aria-label="Choose a photo"
                onChange={(event) => {
                  const picked = event.target.files?.[0] ?? null;
                  event.target.value = "";
                  if (picked) attach(picked);
                }}
              />
              <label htmlFor="ai-message" className="sr-only">
                Message the assistant
              </label>
              <textarea
                id="ai-message"
                ref={textarea}
                value={input}
                rows={1}
                onChange={(event) => setInput(event.target.value)}
                onKeyDown={onKeyDown}
                disabled={pending}
                placeholder={
                  attachment
                    ? "Say something about it (optional)"
                    : "Ask, log or plan…"
                }
                className="block w-full resize-none bg-transparent px-4 pb-1 pt-3 leading-6 text-foreground outline-none placeholder:text-muted-foreground disabled:opacity-60"
              />
              <div className="flex items-center justify-between px-2 pb-2">
                <button
                  type="button"
                  onClick={() => fileInput.current?.click()}
                  disabled={pending}
                  aria-label="Attach a photo"
                  title="Attach a photo"
                  className="flex h-10 w-10 items-center justify-center rounded-sm text-muted-foreground transition-colors hover:bg-muted hover:text-foreground disabled:opacity-60"
                >
                  <Plus aria-hidden="true" size={20} strokeWidth={1.7} />
                </button>
                <button
                  type="submit"
                  disabled={pending || (!input.trim() && !attachment)}
                  aria-label="Send"
                  className="flex h-9 w-9 items-center justify-center rounded-full bg-brand-700 text-white transition-colors hover:bg-brand-800 disabled:cursor-not-allowed disabled:opacity-40 dark:bg-brand-300 dark:text-charcoal-blue-950 dark:hover:bg-brand-200"
                >
                  <ArrowUp aria-hidden="true" size={18} strokeWidth={2} />
                </button>
              </div>
            </div>
            {!phone && (
              <p className="text-xs text-muted-foreground">
                Enter sends. Shift and Enter start a new line.
              </p>
            )}
          </form>
        </>
      )}

      <Lightbox image={lightbox} onClose={() => setLightbox(null)} />

      <ConfirmationModal
        isOpen={deleting !== null}
        onClose={() => setDeleting(null)}
        onConfirm={confirmDelete}
        title="Delete this conversation?"
        message={
          deleting
            ? `"${deleting.title}" and its messages will be removed. This cannot be undone.`
            : ""
        }
        confirmText="Delete"
        isDanger
      />
    </div>
  );
}

function ConversationList({
  threads,
  activeId,
  search,
  onSearch,
  onOpen,
  onDelete,
}: {
  threads: AiChatThread[];
  activeId: string | null;
  search: string;
  onSearch: (value: string) => void;
  onOpen: (id: string) => void;
  onDelete: (thread: AiChatThread) => void;
}) {
  const query = search.trim().toLowerCase();
  const shown = query
    ? threads.filter((t) => t.title.toLowerCase().includes(query))
    : threads;
  const now = new Date();
  const groups = [
    { label: "Today", items: shown.filter((t) => sameDay(t.updatedAt, now)) },
    {
      label: "Earlier",
      items: shown.filter((t) => !sameDay(t.updatedAt, now)),
    },
  ].filter((group) => group.items.length > 0);

  return (
    <section
      aria-label="Conversations"
      className="flex min-h-0 flex-1 animate-overlay-in flex-col"
    >
      <div className="px-4 pb-2 pt-3">
        <label htmlFor="assistant-search" className="sr-only">
          Search conversations
        </label>
        <input
          id="assistant-search"
          type="search"
          value={search}
          onChange={(event) => onSearch(event.target.value)}
          placeholder="Search conversations"
          className="input min-h-11 w-full"
        />
      </div>
      {threads.length === 0 ? (
        <p className="px-4 py-3 text-sm text-muted-foreground">
          Your conversations will be listed here.
        </p>
      ) : groups.length === 0 ? (
        <p className="px-4 py-3 text-sm text-muted-foreground">
          No conversation matches that.
        </p>
      ) : (
        <div className="min-h-0 flex-1 overflow-y-auto px-2 pb-4">
          {groups.map((group) => (
            <div key={group.label} className="pt-3">
              <h3 className="eyebrow px-2 pb-1">{group.label}</h3>
              <ul className="divide-y divide-border border-y border-border">
                {group.items.map((thread) => (
                  <li key={thread.id} className="flex items-center">
                    <button
                      type="button"
                      onClick={() => onOpen(thread.id)}
                      aria-current={thread.id === activeId ? "true" : undefined}
                      className={cn(
                        "min-h-11 min-w-0 flex-1 truncate px-2 py-2.5 text-left text-sm transition-colors hover:bg-muted",
                        thread.id === activeId
                          ? "bg-muted font-semibold"
                          : "text-foreground",
                      )}
                    >
                      {thread.title}
                    </button>
                    <button
                      type="button"
                      onClick={() => onDelete(thread)}
                      aria-label={`Delete ${thread.title}`}
                      className="min-h-11 shrink-0 px-2 text-xs text-destructive transition-colors hover:bg-danger-wash"
                    >
                      Delete
                    </button>
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>
      )}
    </section>
  );
}

/**
 * A reply whose tool call the person's consent refused. One button turns on the switch it named
 * and nothing else; once on, the question can go again.
 */
function PermissionNeeded({
  requirement,
  allowed,
  busy,
  onAllow,
  onSendAgain,
}: {
  requirement: AiConsentRequirement;
  allowed: boolean;
  busy: boolean;
  onAllow: () => void;
  onSendAgain: () => void;
}) {
  const words = describeRequirement(requirement);
  return (
    <div
      role="group"
      aria-label="Permission needed"
      className="w-full max-w-[92%] space-y-2 rounded-md border border-border bg-muted/40 px-3.5 py-3 text-sm transition-opacity duration-200 ease-out starting:opacity-0"
    >
      <p className="font-medium">
        {allowed ? "Allowed" : words.title}
      </p>
      <p className="text-muted-foreground">
        {allowed
          ? "Send the question again and it can do it now."
          : "Turn on just this, and ask again. You can change it any time in Settings."}
      </p>
      {allowed ? (
        <button
          type="button"
          onClick={onSendAgain}
          disabled={busy}
          className="btn-secondary min-h-10"
        >
          Send again
        </button>
      ) : (
        <button
          type="button"
          onClick={onAllow}
          disabled={busy}
          className="btn-primary min-h-10"
        >
          {words.action}
        </button>
      )}
    </div>
  );
}

/**
 * Only addresses a browser can show as a picture reach an image: the preview blob, an inline image, a hosted
 * photo, or a path on this site. Rendered through next/image without optimization, since these are the
 * person's own photos and previews rather than site assets. By default the parent sets the box and `fill`
 * fits it; `inline` keeps the photo's own proportions for a transcript line, bounded by its class names.
 */
function PictureImage({
  src,
  alt,
  className,
  inline = false,
  onClick,
}: {
  src: string;
  alt: string;
  className?: string;
  inline?: boolean;
  onClick?: (event: MouseEvent<HTMLImageElement>) => void;
}) {
  if (!(
    src.startsWith("blob:") ||
    src.startsWith("data:image/") ||
    src.startsWith("https://") ||
    src.startsWith("http://") ||
    src.startsWith("/")
  ))
    return null;
  if (inline)
    return (
      <Image
        src={src}
        alt={alt}
        width={640}
        height={480}
        unoptimized
        loading="lazy"
        className={cn("h-auto w-auto", className)}
        onClick={onClick}
      />
    );
  return (
    <Image
      src={src}
      alt={alt}
      fill
      unoptimized
      sizes="100vw"
      className={className}
      onClick={onClick}
    />
  );
}

function EmptyState({
  onPick,
  disabled,
}: {
  onPick: (prompt: string) => void;
  disabled: boolean;
}) {
  return (
    <div className="flex flex-col gap-4 py-2">
      <div className="space-y-2">
        <h3 className="text-xl font-semibold tracking-tight">
          What would you like to know?
        </h3>
        <p className="max-w-prose text-sm leading-relaxed text-muted-foreground">
          Ask about your day, your week, or your training. You can also send a
          photo of a meal.
        </p>
      </div>
      <div>
        <p className="border-b border-foreground pb-2 text-[10px] font-semibold uppercase tracking-[0.14em] text-muted-foreground">
          Try asking
        </p>
        <ul className="divide-y divide-border">
          {QUICK_PROMPTS.map((quick, index) => (
            <li key={quick.id}>
              <button
                type="button"
                disabled={disabled}
                onClick={() => onPick(quick.prompt)}
                className="flex min-h-14 w-full items-baseline gap-4 py-3 text-left text-sm transition-colors hover:bg-muted disabled:opacity-60"
              >
                <span
                  aria-hidden="true"
                  className="tabular-nums w-6 shrink-0 text-xs text-muted-foreground"
                >
                  {String(index + 1).padStart(2, "0")}
                </span>
                <span className="flex-1">{quick.label}</span>
              </button>
            </li>
          ))}
        </ul>
      </div>
    </div>
  );
}

function MessageView({
  message,
  actions,
  cards,
  onOpenImage,
  children,
}: {
  message: AiChatMessage;
  actions?: AiToolInvocation[];
  cards?: AiCard[];
  onOpenImage: (src: string) => void;
  children?: ReactNode;
}) {
  const [copied, setCopied] = useState(false);
  const mine = message.fromUser;

  async function copy() {
    try {
      await navigator.clipboard.writeText(message.content);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 1500);
    } catch {
      // Copying is a courtesy; a browser that refuses leaves the text selectable.
    }
  }

  return (
    <article
      aria-label={mine ? "You said" : "The assistant replied"}
      className={cn(
        "flex flex-col gap-1 transition-[opacity,translate] duration-200 ease-out starting:translate-y-1 starting:opacity-0",
        mine ? "items-end" : "items-start",
      )}
    >
      <p className="text-xs text-muted-foreground">
        {mine ? "You" : "Assistant"}
        {timeLabel(message.createdAt) && (
          <>
            {" "}
            ·{" "}
            <time dateTime={message.createdAt}>
              {timeLabel(message.createdAt)}
            </time>
          </>
        )}
      </p>
      <div
        className={cn(
          "max-w-[92%] space-y-2 rounded-md px-3.5 py-2.5 text-sm leading-relaxed",
          mine
            ? "whitespace-pre-wrap bg-primary text-primary-foreground"
            : "border border-border bg-background",
        )}
      >
        {message.imageUrl && (
          <button
            type="button"
            onClick={() => onOpenImage(message.imageUrl!)}
            aria-label="Open photo"
            className="block overflow-hidden rounded-sm focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-600"
          >
            <PictureImage
              src={message.imageUrl}
              alt="Photo in the conversation"
              inline
              className="max-h-60 w-auto max-w-full cursor-zoom-in object-contain"
            />
          </button>
        )}
        {/* Only the assistant writes markdown. A user's asterisks are asterisks. */}
        {message.content &&
          (mine ? message.content : <AiMarkdown content={message.content} />)}
      </div>
      {!mine && message.content && (
        <button
          type="button"
          onClick={copy}
          className="flex min-h-9 items-center gap-1.5 px-1 text-xs text-muted-foreground hover:text-foreground"
        >
          {copied ? (
            <Check aria-hidden="true" size={13} strokeWidth={1.9} />
          ) : (
            <Copy aria-hidden="true" size={13} strokeWidth={1.7} />
          )}
          {copied ? "Copied" : "Copy"}
        </button>
      )}
      {cards && <AiCardList cards={cards} />}
      {actions && actions.length > 0 && (
        <ul className="space-y-1">
          {actions.map((action, index) => (
            <li
              key={`${action.tool}-${index}`}
              className={cn(
                "flex items-start gap-2 text-xs",
                action.succeeded
                  ? "text-brand-700 dark:text-brand-300"
                  : "text-burnt-peach-700 dark:text-burnt-peach-300",
              )}
            >
              {action.succeeded ? (
                <Check
                  aria-hidden="true"
                  size={13}
                  strokeWidth={1.9}
                  className="mt-0.5 shrink-0"
                />
              ) : (
                <CircleAlert
                  aria-hidden="true"
                  size={13}
                  strokeWidth={1.7}
                  className="mt-0.5 shrink-0"
                />
              )}
              <span>{action.succeeded ? action.summary : action.error}</span>
            </li>
          ))}
        </ul>
      )}
      {children}
    </article>
  );
}

function Lightbox({
  image,
  onClose,
}: {
  image: { src: string; label: string } | null;
  onClose: () => void;
}) {
  return (
    <Dialog.Root
      open={image !== null}
      onOpenChange={(open) => !open && onClose()}
    >
      <Dialog.Portal>
        <Dialog.Overlay className="fixed inset-0 z-[110] bg-black/80" />
        <Dialog.Content
          aria-describedby={undefined}
          className="fixed inset-0 z-[111] flex items-center justify-center p-4"
          onClick={onClose}
        >
          <Dialog.Title className="sr-only">
            {image?.label ?? "Photo"}
          </Dialog.Title>
          {image && (
            <div
              className="relative h-[90dvh] w-[92vw] max-w-5xl"
              onClick={(event) => event.stopPropagation()}
            >
              <PictureImage
                src={image.src}
                alt={image.label}
                className="rounded-sm object-contain"
              />
            </div>
          )}
          <Dialog.Close
            aria-label="Close photo"
            className="absolute right-3 top-3 flex h-11 w-11 items-center justify-center rounded-sm bg-black/60 text-white hover:bg-black/80"
          >
            <X aria-hidden="true" size={22} strokeWidth={1.7} />
          </Dialog.Close>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
}
