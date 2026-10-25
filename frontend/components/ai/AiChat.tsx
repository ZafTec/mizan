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
} from "react";
import { useRouter } from "next/navigation";
import * as Dialog from "@radix-ui/react-dialog";
import {
  ArrowUp,
  Check,
  CircleAlert,
  Copy,
  History,
  Plus,
  X,
} from "lucide-react";
import AiMarkdown from "@/components/ai/AiMarkdown";
import AiCardList from "@/components/ai/cards/AiCardView";
import ConfirmationModal from "@/components/ConfirmationModal";
import {
  deleteAiChatThread,
  getAiChatThread,
  listAiChatThreads,
  sendAiChatImage,
  sendAiChatMessage,
  type AiCard,
  type AiChatMessage,
  type AiChatThread,
  type AiToolInvocation,
} from "@/lib/api/ai";
import { getErrorMessage } from "@/lib/toast";
import { cn } from "@/lib/utils";

/**
 * Icons are named, not passed. The suggestions come from a server component, and a component cannot cross to the
 * browser, only data can.
 */

export interface QuickPrompt {
  id: string;
  label: string;
  prompt: string;
}

const MAX_ROWS = 6;

function timeLabel(iso: string) {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return "";
  return date.toLocaleTimeString([], { hour: "numeric", minute: "2-digit" });
}

/**
 * The assistant, as a conversation. The list of past conversations sits beside the transcript on a wide screen and
 * behind a button on a narrow one; the composer stays at the bottom either way. A photo is a thumbnail that opens
 * full size, in the transcript and before it is sent.
 */
export default function AiChat({
  quickPrompts,
}: {
  quickPrompts: QuickPrompt[];
}) {
  const router = useRouter();
  const [threads, setThreads] = useState<AiChatThread[]>([]);
  const [threadId, setThreadId] = useState<string | null>(null);
  const [messages, setMessages] = useState<AiChatMessage[]>([]);
  // What each reply did, keyed by message id. Only for this visit: tool invocations are echoes of a turn, not part
  // of the transcript.
  const [performed, setPerformed] = useState<
    Record<string, AiToolInvocation[]>
  >({});
  // The typed cards each reply carried (a plan, a day, a cost table, a list), keyed the same way and for the
  // same reason: they come with the turn, not with the stored transcript.
  const [cards, setCards] = useState<Record<string, AiCard[]>>({});
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
  const [listOpen, setListOpen] = useState(false);
  const [deleting, setDeleting] = useState<AiChatThread | null>(null);
  const transcript = useRef<HTMLDivElement>(null);
  const textarea = useRef<HTMLTextAreaElement>(null);
  const fileInput = useRef<HTMLInputElement>(null);
  const stickToBottom = useRef(true);

  useEffect(() => {
    // Past conversations, so the screen opens on something rather than pretending nothing was ever said.
    listAiChatThreads()
      .then(setThreads)
      .catch(() => setThreads([]));
  }, []);

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

  function openThread(id: string) {
    setError(null);
    setListOpen(false);
    startTransition(async () => {
      try {
        const thread = await getAiChatThread(id);
        setThreadId(thread.id);
        setMessages(thread.messages);
        stickToBottom.current = true;
      } catch (cause) {
        setError(getErrorMessage(cause, "Could not open that conversation."));
      }
    });
  }

  function startNew() {
    setThreadId(null);
    setMessages([]);
    setError(null);
    setListOpen(false);
    textarea.current?.focus();
  }

  function confirmDelete() {
    const thread = deleting;
    if (!thread) return;
    setDeleting(null);
    startTransition(async () => {
      try {
        await deleteAiChatThread(thread.id);
        setThreads((current) => current.filter((t) => t.id !== thread.id));
        if (threadId === thread.id) startNew();
      } catch (cause) {
        setError(getErrorMessage(cause, "Could not delete that conversation."));
      }
    });
  }

  function attach(file: File | null) {
    if (attachment) release(attachment.url);
    setAttachment(file ? { file, url: preview(file) } : null);
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
        setThreadId(turn.threadId);
        // The stored message takes the optimistic one's place, so the photo now comes from its stored address
        // and the local preview can go.
        setMessages((current) => [
          ...current.map((m) => (m.id === optimisticId ? turn.message : m)),
          turn.reply,
        ]);
        if (photo) release(photo.url);
        if (turn.cards?.length)
          setCards((current) => ({ ...current, [turn.reply.id]: turn.cards }));
        if (turn.performed?.length) {
          setPerformed((current) => ({
            ...current,
            [turn.reply.id]: turn.performed,
          }));
          // Something was written, so anything on screen behind this is stale.
          router.refresh();
        }
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

  const list = (
    <ConversationList
      threads={threads}
      activeId={threadId}
      onNew={startNew}
      onOpen={openThread}
      onDelete={setDeleting}
    />
  );

  return (
    <div className="grid h-[calc(100dvh-17.5rem-env(safe-area-inset-bottom,0px))] min-h-[26rem] gap-6 lg:h-[calc(100dvh-13rem)] lg:grid-cols-[16rem_minmax(0,1fr)]">
      <aside
        aria-label="Conversations"
        className="hidden min-h-0 flex-col lg:flex"
      >
        {list}
      </aside>

      <section
        aria-label="Assistant"
        className="flex min-h-0 flex-col border-y border-border bg-card max-sm:-mx-5 sm:rounded-md sm:border"
      >
        <header className="flex items-center justify-between gap-3 border-b border-border px-4 py-3">
          <div className="min-w-0">
            <h2 className="truncate text-base font-semibold">
              {threads.find((t) => t.id === threadId)?.title ??
                "New conversation"}
            </h2>
            <p className="hidden text-xs text-muted-foreground sm:block">
              Answers use only what you chose to share.
            </p>
          </div>
          <div className="flex shrink-0 items-center lg:hidden">
            <button
              type="button"
              onClick={startNew}
              aria-label="New conversation"
              title="New conversation"
              className="flex h-11 w-11 items-center justify-center rounded-sm hover:bg-muted"
            >
              <Plus aria-hidden="true" size={20} strokeWidth={1.7} />
            </button>
            <Dialog.Root open={listOpen} onOpenChange={setListOpen}>
              <Dialog.Trigger
                aria-label="Conversations"
                title="Conversations"
                className="flex h-11 w-11 items-center justify-center rounded-sm hover:bg-muted"
              >
                <History aria-hidden="true" size={20} strokeWidth={1.7} />
              </Dialog.Trigger>
              <Dialog.Portal>
                <Dialog.Overlay className="fixed inset-0 z-[100] bg-charcoal-blue-950/45" />
                <Dialog.Content className="fixed inset-x-0 bottom-0 z-[101] flex max-h-[80dvh] flex-col rounded-t-md border border-border bg-background p-5 pb-[calc(1.25rem+env(safe-area-inset-bottom,0px))]">
                  <div className="mb-3 flex items-center justify-between">
                    <Dialog.Title className="text-lg font-semibold">
                      Conversations
                    </Dialog.Title>
                    <Dialog.Close
                      aria-label="Close conversations"
                      className="-mr-2 flex h-11 w-11 items-center justify-center rounded-sm hover:bg-muted"
                    >
                      <X aria-hidden="true" size={20} strokeWidth={1.7} />
                    </Dialog.Close>
                  </div>
                  <Dialog.Description className="sr-only">
                    Open an earlier conversation or start a new one.
                  </Dialog.Description>
                  {list}
                </Dialog.Content>
              </Dialog.Portal>
            </Dialog.Root>
          </div>
        </header>

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
          {messages.length === 0 ? (
            <EmptyState
              quickPrompts={quickPrompts}
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
              />
            ))
          )}

          {pending && (
            <div
              role="status"
              className="flex items-center gap-2 text-sm text-muted-foreground"
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
              className="flex items-start gap-2 text-sm text-burnt-peach-700 dark:text-burnt-peach-300"
            >
              <CircleAlert
                aria-hidden="true"
                size={16}
                strokeWidth={1.7}
                className="mt-0.5 shrink-0"
              />
              {error}
            </p>
          )}
        </div>

        <form
          onSubmit={onSubmit}
          className="space-y-2 border-t border-border p-3 pb-[calc(0.75rem+env(safe-area-inset-bottom,0px))] sm:p-4"
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
                  : "Ask the assistant"
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
          <p className="hidden text-xs text-muted-foreground sm:block">
            Enter sends. Shift and Enter start a new line.
          </p>
        </form>
      </section>

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
  onNew,
  onOpen,
  onDelete,
}: {
  threads: AiChatThread[];
  activeId: string | null;
  onNew: () => void;
  onOpen: (id: string) => void;
  onDelete: (thread: AiChatThread) => void;
}) {
  return (
    <div className="flex min-h-0 flex-1 flex-col gap-3">
      <button
        type="button"
        onClick={onNew}
        className="btn-secondary min-h-11 w-full"
      >
        New conversation
      </button>
      {threads.length === 0 ? (
        <p className="px-1 text-sm text-muted-foreground">
          Your conversations will be listed here.
        </p>
      ) : (
        <ul className="min-h-0 flex-1 divide-y divide-border overflow-y-auto border-y border-border">
          {threads.map((thread) => (
            <li key={thread.id} className="flex items-center">
              <button
                type="button"
                onClick={() => onOpen(thread.id)}
                aria-current={thread.id === activeId ? "true" : undefined}
                className={cn(
                  "min-h-11 min-w-0 flex-1 truncate px-2 py-2.5 text-left text-sm transition-colors hover:bg-muted",
                  thread.id === activeId
                    ? "bg-muted font-semibold"
                    : "text-muted-foreground",
                )}
              >
                {thread.title}
              </button>
              <button
                type="button"
                onClick={() => onDelete(thread)}
                aria-label={`Delete ${thread.title}`}
                className="min-h-11 shrink-0 px-2 text-xs text-muted-foreground hover:text-destructive"
              >
                Delete
              </button>
            </li>
          ))}
        </ul>
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
  quickPrompts,
  onPick,
  disabled,
}: {
  quickPrompts: QuickPrompt[];
  onPick: (prompt: string) => void;
  disabled: boolean;
}) {
  return (
    <div className="mx-auto flex max-w-xl flex-col gap-4 py-3 sm:py-10">
      <div className="space-y-2">
        <h3 className="text-xl font-semibold tracking-tight sm:text-2xl">
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
          {quickPrompts.map((quick, index) => (
            <li key={quick.id}>
              <button
                type="button"
                disabled={disabled}
                onClick={() => onPick(quick.prompt)}
                className="flex min-h-14 w-full items-baseline gap-4 py-3 text-left text-sm transition-colors hover:bg-muted disabled:opacity-60"
              >
                <span
                  aria-hidden="true"
                  className="num w-6 shrink-0 text-xs text-muted-foreground"
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
}: {
  message: AiChatMessage;
  actions?: AiToolInvocation[];
  cards?: AiCard[];
  onOpenImage: (src: string) => void;
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
      className={cn("flex flex-col gap-1", mine ? "items-end" : "items-start")}
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
          "max-w-[92%] space-y-2 rounded-md px-3.5 py-2.5 text-sm leading-relaxed sm:max-w-[80%]",
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
