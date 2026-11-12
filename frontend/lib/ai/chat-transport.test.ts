import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "@/lib/api";
import * as api from "@/lib/api/ai";
import {
  ChatTransportUnsupported,
  chatErrorFrom,
  mizanChatTransport,
  type ChatEvent,
} from "./chat-transport";

vi.mock("@/lib/api/ai", () => ({
  listAiChatThreads: vi.fn(),
  getAiChatThread: vi.fn(),
  deleteAiChatThread: vi.fn(),
  sendAiChatMessage: vi.fn(),
  sendAiChatImage: vi.fn(),
}));

const now = Date.parse("2026-10-25T23:00:00Z");
const failure = (status: number, body: unknown) =>
  new ApiError(status, "", body);

describe("chatErrorFrom", () => {
  it.each([
    [
      "a spent allowance, waiting until it resets",
      failure(429, {
        errorCode: "ai_quota_exceeded",
        error: "You have used your AI allowance for now.",
        resetsAt: "2026-10-26T00:00:00Z",
      }),
      {
        kind: "limit",
        code: "ai_quota_exceeded",
        message: "You have used your AI allowance for now.",
        retryAfter: 3600,
      },
    ],
    [
      "too many at once",
      failure(429, { errorCode: "rate_limited", error: "Slow down." }),
      { kind: "rate_limited", code: "rate_limited", message: "Slow down." },
    ],
    [
      "content the provider refused",
      failure(422, {
        errorCode: "ai_blocked",
        error: "I can't help with that.",
      }),
      {
        kind: "blocked",
        code: "ai_blocked",
        message: "I can't help with that.",
      },
    ],
    [
      "a person the provider will not serve",
      failure(403, {
        errorCode: "ai_account_blocked",
        error: "The assistant isn't available for your account.",
      }),
      {
        kind: "blocked",
        code: "ai_account_blocked",
        message: "The assistant isn't available for your account.",
      },
    ],
    [
      "the provider's own limit, waiting until it resets",
      failure(503, {
        errorCode: "ai_limit_reached",
        error: "The assistant has reached its limit for now.",
        resetsAt: "2026-10-26T00:00:00Z",
      }),
      {
        kind: "limit",
        code: "ai_limit_reached",
        message: "The assistant has reached its limit for now.",
        retryAfter: 3600,
      },
    ],
    [
      "the provider asking to slow down, with its wait",
      failure(429, {
        errorCode: "ai_rate_limited",
        error: "The assistant is busy right now. Try again in 30 seconds.",
        retryAfterSeconds: 30,
      }),
      {
        kind: "rate_limited",
        code: "ai_rate_limited",
        message: "The assistant is busy right now. Try again in 30 seconds.",
        retryAfter: 30,
      },
    ],
    [
      "an unavailable provider",
      failure(503, { errorCode: "ai_unavailable", error: "Not configured." }),
      {
        kind: "unavailable",
        code: "ai_unavailable",
        message: "Not configured.",
      },
    ],
    [
      "any other refusal, in the server's words",
      failure(400, { errorCode: "validation_failed", error: "Too long." }),
      { kind: "other", code: "validation_failed", message: "Too long." },
    ],
    [
      "a network failure",
      new Error("Failed to fetch"),
      { kind: "other", code: "failed", message: "Failed to fetch" },
    ],
    [
      "something this transport cannot do",
      new ChatTransportUnsupported("a tool result"),
      {
        kind: "other",
        code: "not_supported",
        message: "The assistant cannot take a tool result here.",
      },
    ],
  ])("reads %s", (_, cause, expected) => {
    expect(chatErrorFrom(cause, now)).toEqual(expected);
  });
});

async function collect(events: AsyncIterable<ChatEvent>) {
  const seen: ChatEvent[] = [];
  for await (const event of events) seen.push(event);
  return seen;
}

describe("Mizan's chat transport", () => {
  beforeEach(() => vi.clearAllMocks());

  it("tells a stored turn as events, ending with the stored messages", async () => {
    const message = {
      id: "u1",
      fromUser: true,
      content: "log eggs",
      createdAt: "2026-10-01T09:59:00Z",
    };
    const reply = {
      id: "r1",
      fromUser: false,
      content: "Logged.",
      createdAt: "2026-10-01T10:00:00Z",
    };
    vi.mocked(api.sendAiChatMessage).mockResolvedValue({
      threadId: "t1",
      title: "Eggs",
      message,
      reply,
      performed: [
        { tool: "log_food", summary: "Logged two eggs", succeeded: true },
        {
          tool: "log_body",
          summary: "",
          succeeded: false,
          error: "No weight given",
        },
      ],
      cards: [],
      consentRequired: [
        { errorCode: "ai_consent_required", axis: "body", access: "write" },
      ],
    });

    const events = await collect(
      mizanChatTransport.send(
        null,
        { text: "log eggs" },
        { idempotencyKey: "k1" },
      ),
    );

    expect(api.sendAiChatMessage).toHaveBeenCalledWith(null, "log eggs", "k1");
    expect(events).toEqual([
      { type: "turn_started", threadId: null },
      {
        type: "tool_finished",
        callId: "r1-0",
        name: "log_food",
        succeeded: true,
        label: "Logged two eggs",
      },
      {
        type: "tool_finished",
        callId: "r1-1",
        name: "log_body",
        succeeded: false,
        label: "No weight given",
      },
      {
        type: "consent_required",
        requirements: [
          { errorCode: "ai_consent_required", axis: "body", access: "write" },
        ],
      },
      { type: "text", delta: "Logged." },
      { type: "turn_finished", threadId: "t1", title: "Eggs", message, reply },
    ]);
  });

  it("ends a failed photo turn with an error event that keeps the key", async () => {
    vi.mocked(api.sendAiChatImage).mockRejectedValue(
      failure(429, { errorCode: "ai_quota_exceeded", error: "Spent." }),
    );
    const photo = new File([new Uint8Array([1])], "a.jpg");

    const events = await collect(
      mizanChatTransport.send(
        "t1",
        { text: "", image: photo },
        { idempotencyKey: "k2" },
      ),
    );

    expect(api.sendAiChatImage).toHaveBeenCalledWith("t1", "", photo, "k2");
    expect(events.at(-1)).toMatchObject({
      type: "error",
      kind: "limit",
      message: "Spent.",
    });
  });

  it("does not take confirmations or tool results yet", async () => {
    await expect(mizanChatTransport.confirm("t1", "c1", true)).rejects.toThrow(
      ChatTransportUnsupported,
    );
    await expect(
      mizanChatTransport.submitToolResult("t1", "c1", {}),
    ).rejects.toThrow(ChatTransportUnsupported);
  });
});
