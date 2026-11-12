import { ApiError } from "@/lib/api";
import { readAiProblem } from "@/lib/ai/problem";
import {
  deleteAiChatThread,
  getAiChatThread,
  listAiChatThreads,
  sendAiChatImage,
  sendAiChatMessage,
  type AiCard,
  type AiChatMessage,
  type AiChatThread,
  type AiChatThreadDetail,
  type AiChatTurn,
  type AiConsentRequirement,
} from "@/lib/api/ai";
import { getErrorMessage } from "@/lib/toast";

/**
 * Where the assistant's turns come from, kept apart from the chat that shows them. The chat reads a
 * turn as a stream of events, so a transport that streams (a conversation API with text deltas,
 * tool activity, confirmations and a client tool that pauses the run) fits behind the same
 * interface as today's one-response endpoints. Records the API already defines keep their
 * generated types.
 */

/**
 * Why a turn failed, in terms the chat can act on: `blocked` is a refusal to help, `limit` an
 * allowance spent, `rate_limited` too many too fast. `retryAfter` is in seconds.
 */
export type ChatErrorKind =
  "limit" | "blocked" | "rate_limited" | "unavailable" | "other";

export interface ChatError {
  code: string;
  message: string;
  retryAfter?: number;
  kind: ChatErrorKind;
}

export interface ChatConfirmationField {
  label: string;
  value: string;
}

export type ChatEvent =
  | { type: "turn_started"; threadId: string | null }
  | { type: "text"; delta: string }
  | { type: "tool_started"; callId: string; name: string; label: string }
  | {
      type: "tool_finished";
      callId: string;
      name: string;
      succeeded: boolean;
      /** What it did, or why it did not, in plain words. */
      label: string;
    }
  | {
      type: "confirmation_requested";
      callId: string;
      question: string;
      fields: ChatConfirmationField[];
      /** ISO time after which an answer is no longer taken; null when it does not expire. */
      expiresAt: string | null;
    }
  | {
      type: "client_tool_call";
      callId: string;
      name: string;
      arguments: Record<string, unknown>;
    }
  | { type: "cards"; cards: AiCard[] }
  | { type: "consent_required"; requirements: AiConsentRequirement[] }
  | {
      type: "turn_finished";
      threadId: string;
      title: string;
      /** The person's message as stored, replacing the one shown while it was sent. */
      message: AiChatMessage;
      reply: AiChatMessage;
    }
  | ({ type: "error" } & ChatError);

export interface ChatInput {
  text: string;
  image?: File;
}

export interface ChatSendOptions {
  signal?: AbortSignal;
  /** The same key for a retry of the same attempt, so a lost response is not acted on twice. */
  idempotencyKey: string;
}

export interface ChatTransport {
  /** One turn. Failures arrive as an `error` event; the stream ends after `turn_finished` or `error`. */
  send(
    threadId: string | null,
    input: ChatInput,
    options: ChatSendOptions,
  ): AsyncIterable<ChatEvent>;
  /** Answers a `confirmation_requested`; the run continues on the turn's stream. */
  confirm(threadId: string, callId: string, approve: boolean): Promise<void>;
  /** Answers a `client_tool_call`; the run continues on the turn's stream. */
  submitToolResult(
    threadId: string,
    callId: string,
    result: unknown,
  ): Promise<void>;
  list(): Promise<AiChatThread[]>;
  load(id: string): Promise<AiChatThreadDetail>;
  remove(id: string): Promise<void>;
}

/** A transport asked for something it cannot do, such as answering a confirmation it never asks. */
export class ChatTransportUnsupported extends Error {
  readonly code = "not_supported";
  constructor(what: string) {
    super(`The assistant cannot take ${what} here.`);
    this.name = "ChatTransportUnsupported";
  }
}

const FALLBACK = "Chat request failed.";

function errorCode(body: unknown): string | null {
  if (typeof body !== "object" || body === null) return null;
  const code = (body as Record<string, unknown>).errorCode;
  return typeof code === "string" && code ? code : null;
}

function bodyText(body: unknown, name: string): string | null {
  if (typeof body !== "object" || body === null) return null;
  const value = (body as Record<string, unknown>)[name];
  return typeof value === "string" && value ? value : null;
}

function bodySeconds(body: unknown, name: string): number | undefined {
  if (typeof body !== "object" || body === null) return undefined;
  const value = (body as Record<string, unknown>)[name];
  return typeof value === "number" && value > 0 ? value : undefined;
}

function secondsUntil(iso: string | null, now: number): number | undefined {
  if (!iso) return undefined;
  const at = Date.parse(iso);
  if (Number.isNaN(at)) return undefined;
  return Math.max(1, Math.ceil((at - now) / 1000));
}

/** Reads a failed call as a chat error. The server's own words are kept wherever it gave some. */
export function chatErrorFrom(cause: unknown, now = Date.now()): ChatError {
  if (cause instanceof ChatTransportUnsupported)
    return { kind: "other", code: cause.code, message: cause.message };
  if (!(cause instanceof ApiError))
    return {
      kind: "other",
      code: "failed",
      message: getErrorMessage(cause, FALLBACK),
    };

  const code = errorCode(cause.body) ?? `http_${cause.status}`;
  // The provider's refusals (docs/AI.md#provider-errors), each in the API's own words.
  if (code === "ai_blocked" || code === "ai_account_blocked")
    return { kind: "blocked", code, message: getErrorMessage(cause, FALLBACK) };
  if (code === "ai_limit_reached")
    return {
      kind: "limit",
      code,
      message: getErrorMessage(cause, FALLBACK),
      retryAfter: secondsUntil(bodyText(cause.body, "resetsAt"), now),
    };
  if (code === "ai_rate_limited")
    return {
      kind: "rate_limited",
      code,
      message: getErrorMessage(cause, FALLBACK),
      retryAfter: bodySeconds(cause.body, "retryAfterSeconds"),
    };
  const problem = readAiProblem(cause);
  if (problem?.kind === "quota")
    return {
      kind: "limit",
      code,
      message: problem.message,
      retryAfter: secondsUntil(problem.resetsAt, now),
    };
  if (problem?.kind === "unavailable")
    return { kind: "unavailable", code, message: problem.message };
  // Any other 429 is Mizan's own pace limit, not allowance.
  if (cause.status === 429)
    return {
      kind: "rate_limited",
      code,
      message: getErrorMessage(cause, "Too many messages at once."),
    };
  return { kind: "other", code, message: getErrorMessage(cause, FALLBACK) };
}

/** Today's single response, told as the events a streaming turn would carry. */
function* turnEvents(turn: AiChatTurn): Generator<ChatEvent> {
  for (const [index, action] of (turn.performed ?? []).entries())
    yield {
      type: "tool_finished",
      callId: `${turn.reply.id}-${index}`,
      name: action.tool,
      succeeded: action.succeeded,
      label: action.succeeded ? action.summary : (action.error ?? ""),
    };
  if (turn.cards?.length) yield { type: "cards", cards: turn.cards };
  if (turn.consentRequired?.length)
    yield { type: "consent_required", requirements: turn.consentRequired };
  if (turn.reply.content) yield { type: "text", delta: turn.reply.content };
  yield {
    type: "turn_finished",
    threadId: turn.threadId,
    title: turn.title,
    message: turn.message,
    reply: turn.reply,
  };
}

/** Mizan's own chat endpoints: one JSON response per turn, no confirmations, no client tools. */
export const mizanChatTransport: ChatTransport = {
  async *send(threadId, input, { signal, idempotencyKey }) {
    yield { type: "turn_started", threadId };
    let turn: AiChatTurn;
    try {
      turn = input.image
        ? await sendAiChatImage(
            threadId,
            input.text,
            input.image,
            idempotencyKey,
          )
        : await sendAiChatMessage(threadId, input.text, idempotencyKey);
    } catch (cause) {
      if (!signal?.aborted) yield { type: "error", ...chatErrorFrom(cause) };
      return;
    }
    if (signal?.aborted) return;
    yield* turnEvents(turn);
  },
  async confirm() {
    throw new ChatTransportUnsupported("an answer to a confirmation");
  },
  async submitToolResult() {
    throw new ChatTransportUnsupported("a tool result");
  },
  list: () => listAiChatThreads(),
  load: (id) => getAiChatThread(id),
  remove: (id) => deleteAiChatThread(id),
};
