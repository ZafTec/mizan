import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  act,
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import { useState } from "react";
import AiChat, { NO_ACCESS_LINE } from "./AiChat";
import * as api from "@/lib/api/ai";
import type { ChatEvent, ChatTransport } from "@/lib/ai/chat-transport";

const { refresh } = vi.hoisted(() => ({ refresh: vi.fn() }));
vi.mock("next/navigation", () => ({ useRouter: () => ({ refresh }) }));
vi.mock("@/lib/api/ai", () => ({
  listAiChatThreads: vi.fn(),
  getAiChatThread: vi.fn(),
  deleteAiChatThread: vi.fn(),
  sendAiChatMessage: vi.fn(),
  sendAiChatImage: vi.fn(),
  getAiConsent: vi.fn(),
  updateAiConsent: vi.fn(),
}));

const reply = (content: string, extra: object = {}) => ({
  id: crypto.randomUUID(),
  fromUser: false,
  content,
  createdAt: "2026-10-01T10:00:00Z",
  ...extra,
});
const turn = (
  content: string,
  performed: api.AiToolInvocation[] = [],
  message: Partial<api.AiChatMessage> = {},
): api.AiChatTurn => ({
  threadId: "t1",
  title: "Macros",
  message: {
    id: crypto.randomUUID(),
    fromUser: true,
    content: "",
    createdAt: "2026-10-01T09:59:00Z",
    ...message,
  },
  reply: reply(content),
  performed,
  cards: [],
  consentRequired: [],
});

const consent = (overrides: Partial<api.AiConsent> = {}): api.AiConsent => ({
  enabled: true,
  shareNutrition: true,
  shareTraining: true,
  shareBody: true,
  allowWrites: true,
  writeNutrition: true,
  writeTraining: true,
  writeBody: true,
  updatedAt: "2026-10-01T09:00:00Z",
  ...overrides,
});
const nothingChosen = consent({
  enabled: false,
  shareNutrition: false,
  shareTraining: false,
  shareBody: false,
  allowWrites: false,
  writeNutrition: false,
  writeTraining: false,
  writeBody: false,
});

/** The panel holds the open conversation; this stands in for it. */
function Panel({ initial = null }: { initial?: string | null }) {
  const [threadId, setThreadId] = useState<string | null>(initial);
  return (
    <AiChat threadId={threadId} onThreadChange={setThreadId} onClose={close} />
  );
}
const close = vi.fn();
/** Renders the panel's conversation and lets its first reads (threads, consent) settle. */
async function chat() {
  render(<Panel />);
  await act(async () => {});
}

async function openPast(title: string) {
  fireEvent.click(screen.getByRole("button", { name: "Past conversations" }));
  fireEvent.click(await screen.findByRole("button", { name: title }));
}

beforeEach(() => {
  vi.clearAllMocks();
  window.localStorage.clear();
  vi.mocked(api.getAiConsent).mockResolvedValue(consent());
  vi.mocked(api.listAiChatThreads).mockResolvedValue([
    { id: "t0", title: "Last week", updatedAt: "2026-09-24T10:00:00Z" },
  ]);
  vi.mocked(api.getAiChatThread).mockResolvedValue({
    id: "t0",
    title: "Last week",
    updatedAt: "2026-09-24T10:00:00Z",
    messages: [
      {
        id: "m1",
        fromUser: true,
        content: "Is this ok?",
        createdAt: "2026-09-24T10:00:00Z",
        imageUrl: "https://cdn.test/plate.jpg",
      },
      reply("It looks balanced.", { id: "m2" }),
    ],
  });
  vi.stubGlobal(
    "URL",
    Object.assign(URL, {
      createObjectURL: vi.fn(() => "blob:preview"),
      revokeObjectURL: vi.fn(),
    }),
  );
  Element.prototype.scrollTo = vi.fn();
});
afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

const box = () =>
  screen.getByLabelText("Message the assistant") as HTMLTextAreaElement;

describe("the assistant conversation", () => {
  it("opens on suggestions, and a suggestion sends as a turn", async () => {
    vi.mocked(api.sendAiChatMessage).mockResolvedValue(
      turn("Try a chicken wrap."),
    );
    await chat();

    fireEvent.click(
      await screen.findByRole("button", { name: /remaining macros today/ }),
    );

    await screen.findByText("Try a chicken wrap.");
    expect(api.sendAiChatMessage).toHaveBeenCalledWith(
      null,
      "What are 3 quick meal ideas that fit my remaining macros for today?",
      expect.any(String),
    );
    const log = screen.getByRole("log", { name: "Conversation" });
    expect(within(log).getByRole("article", { name: "You said" })).toBeTruthy();
    expect(
      within(log).getByRole("article", { name: "The assistant replied" }),
    ).toBeTruthy();
  });

  it("sends on Enter, keeps a new line on Shift and Enter, and ignores an empty box", async () => {
    vi.mocked(api.sendAiChatMessage).mockResolvedValue(turn("Sure."));
    await chat();

    fireEvent.keyDown(box(), { key: "Enter" });
    expect(api.sendAiChatMessage).not.toHaveBeenCalled();

    fireEvent.change(box(), { target: { value: "hello" } });
    fireEvent.keyDown(box(), { key: "Enter", shiftKey: true });
    expect(api.sendAiChatMessage).not.toHaveBeenCalled();

    fireEvent.keyDown(box(), { key: "Enter" });
    await waitFor(() =>
      expect(api.sendAiChatMessage).toHaveBeenCalledWith(
        null,
        "hello",
        expect.any(String),
      ),
    );
    await screen.findByText("Sure.");
    expect(box().value).toBe("");
  });

  it("gives the person their words back when a turn fails, and says why", async () => {
    vi.mocked(api.sendAiChatMessage).mockRejectedValue(
      new Error("You have used today's assistant allowance."),
    );
    await chat();

    fireEvent.change(box(), { target: { value: "a long question" } });
    fireEvent.click(screen.getByRole("button", { name: "Send" }));

    expect((await screen.findByRole("alert")).textContent).toContain(
      "allowance",
    );
    expect(box().value).toBe("a long question");
    expect(screen.queryByRole("article", { name: "You said" })).toBeNull();
  });

  it("shows what a reply did, and refreshes the page behind it", async () => {
    vi.mocked(api.sendAiChatMessage).mockResolvedValue(
      turn("Logged it.", [
        {
          tool: "log_meal",
          summary: "Logged Greek yogurt (150 kcal)",
          succeeded: true,
        },
      ]),
    );
    await chat();

    fireEvent.change(box(), { target: { value: "log yogurt" } });
    fireEvent.click(screen.getByRole("button", { name: "Send" }));

    await screen.findByText("Logged Greek yogurt (150 kcal)");
    expect(refresh).toHaveBeenCalled();
  });

  it("renders the assistant's markdown and the person's own text literally", async () => {
    vi.mocked(api.sendAiChatMessage).mockResolvedValue(
      turn("You are **on track**.", [], { content: "is *this* fine" }),
    );
    await chat();

    fireEvent.change(box(), { target: { value: "is *this* fine" } });
    fireEvent.click(screen.getByRole("button", { name: "Send" }));

    expect((await screen.findByText("on track")).tagName).toBe("STRONG");
    expect(screen.getByText("is *this* fine")).toBeTruthy();
  });
});

describe("photos", () => {
  const photo = () =>
    new File([new Uint8Array([0xff, 0xd8, 0xff])], "lunch.jpg", {
      type: "image/jpeg",
    });

  it("shows a thumbnail before sending, which can be previewed full size or removed", async () => {
    await chat();

    fireEvent.change(screen.getByLabelText("Choose a photo"), {
      target: { files: [photo()] },
    });

    expect(screen.getByText("lunch.jpg")).toBeTruthy();
    fireEvent.click(
      screen.getByRole("button", { name: "Preview the photo to send" }),
    );
    expect(screen.getByRole("dialog")).toBeTruthy();
    expect(
      within(screen.getByRole("dialog")).getByRole("img", {
        name: "Photo to send",
      }),
    ).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Close photo" }));
    expect(screen.queryByRole("dialog")).toBeNull();

    fireEvent.click(screen.getByRole("button", { name: "Remove photo" }));
    expect(screen.queryByText("lunch.jpg")).toBeNull();
  });

  it("sends a photo on its own, with no words", async () => {
    vi.mocked(api.sendAiChatImage).mockResolvedValue(
      turn("That looks like rice and beans."),
    );
    await chat();

    fireEvent.change(screen.getByLabelText("Choose a photo"), {
      target: { files: [photo()] },
    });
    fireEvent.click(screen.getByRole("button", { name: "Send" }));

    await screen.findByText("That looks like rice and beans.");
    expect(api.sendAiChatImage).toHaveBeenCalledWith(
      null,
      "",
      expect.any(File),
      expect.any(String),
    );
  });

  it("keeps the preview until the stored photo replaces it, then releases it", async () => {
    let answer!: (value: api.AiChatTurn) => void;
    vi.mocked(api.sendAiChatImage).mockReturnValue(
      new Promise<api.AiChatTurn>((resolve) => {
        answer = resolve;
      }),
    );
    await chat();

    fireEvent.change(screen.getByLabelText("Choose a photo"), {
      target: { files: [photo()] },
    });
    fireEvent.change(box(), { target: { value: "lunch" } });
    fireEvent.click(screen.getByRole("button", { name: "Send" }));

    // While the server thinks, the sent line shows the local preview and nothing has been released.
    const mine = await screen.findByRole("article", { name: "You said" });
    expect(
      (
        within(mine).getByRole("img", {
          name: "Photo in the conversation",
        }) as HTMLImageElement
      ).src,
    ).toBe("blob:preview");
    expect(screen.queryByText("lunch.jpg")).toBeNull();
    expect(URL.revokeObjectURL).not.toHaveBeenCalled();

    answer(
      turn("Rice and beans.", [], {
        content: "lunch",
        imageUrl: "https://cdn.test/lunch.jpg",
      }),
    );

    await screen.findByText("Rice and beans.");
    const sent = screen.getByRole("article", { name: "You said" });
    expect(
      (
        within(sent).getByRole("img", {
          name: "Photo in the conversation",
        }) as HTMLImageElement
      ).src,
    ).toBe("https://cdn.test/lunch.jpg");
    expect(URL.revokeObjectURL).toHaveBeenCalledWith("blob:preview");
    expect(URL.createObjectURL).toHaveBeenCalledTimes(1);
  });

  it("gives the same photo back when the turn fails, without making a new preview", async () => {
    vi.mocked(api.sendAiChatImage).mockRejectedValue(
      new Error("The assistant is unavailable."),
    );
    await chat();

    fireEvent.change(screen.getByLabelText("Choose a photo"), {
      target: { files: [photo()] },
    });
    fireEvent.click(screen.getByRole("button", { name: "Send" }));

    await screen.findByRole("alert");
    expect(screen.getByText("lunch.jpg")).toBeTruthy();
    expect(URL.createObjectURL).toHaveBeenCalledTimes(1);
    expect(URL.revokeObjectURL).not.toHaveBeenCalled();

    // Removing it is what releases it.
    fireEvent.click(screen.getByRole("button", { name: "Remove photo" }));
    expect(URL.revokeObjectURL).toHaveBeenCalledWith("blob:preview");
  });

  it("shows a photo in a past conversation only through the guarded image", async () => {
    vi.mocked(api.getAiChatThread).mockResolvedValue({
      id: "t0",
      title: "Last week",
      updatedAt: "2026-09-24T10:00:00Z",
      messages: [
        {
          id: "m1",
          fromUser: true,
          content: "Look",
          createdAt: "2026-09-24T10:00:00Z",
          imageUrl: "javascript:alert(1)",
        },
        reply("I cannot see that.", { id: "m2" }),
      ],
    });
    await chat();
    await openPast("Last week");
    await screen.findByText("I cannot see that.");

    expect(
      screen.queryByRole("img", { name: "Photo in the conversation" }),
    ).toBeNull();
  });

  it("opens a photo in a past conversation full size, and closes it with Escape", async () => {
    await chat();
    await openPast("Last week");
    await screen.findByText("It looks balanced.");

    fireEvent.click(screen.getByRole("button", { name: "Open photo" }));

    const dialog = screen.getByRole("dialog");
    expect(
      (
        within(dialog).getByRole("img", {
          name: "Photo you sent",
        }) as HTMLImageElement
      ).src,
    ).toBe("https://cdn.test/plate.jpg");
    fireEvent.keyDown(dialog, { key: "Escape" });
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
  });
});

describe("conversations", () => {
  it("lists earlier ones, opens one, and starts a new one", async () => {
    await chat();

    await openPast("Last week");
    await screen.findByText("It looks balanced.");
    expect(api.getAiChatThread).toHaveBeenCalledWith("t0");

    fireEvent.click(screen.getByRole("button", { name: "New conversation" }));
    expect(screen.queryByText("It looks balanced.")).toBeNull();
    expect(screen.getByText("What would you like to know?")).toBeTruthy();
  });

  it("asks before deleting a conversation, and only deletes when confirmed", async () => {
    vi.mocked(api.deleteAiChatThread).mockResolvedValue(undefined);
    await chat();

    fireEvent.click(screen.getByRole("button", { name: "Past conversations" }));
    fireEvent.click(
      await screen.findByRole("button", { name: "Delete Last week" }),
    );
    expect(api.deleteAiChatThread).not.toHaveBeenCalled();
    fireEvent.click(await screen.findByRole("button", { name: "Delete" }));

    await waitFor(() =>
      expect(api.deleteAiChatThread).toHaveBeenCalledWith("t0"),
    );
    await waitFor(() =>
      expect(
        screen.queryAllByRole("button", { name: "Delete Last week" }),
      ).toHaveLength(0),
    );
  });

  it("copies a reply", async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.assign(navigator, { clipboard: { writeText } });
    await chat();
    await openPast("Last week");
    await screen.findByText("It looks balanced.");

    fireEvent.click(screen.getByRole("button", { name: /Copy/ }));

    await waitFor(() =>
      expect(writeText).toHaveBeenCalledWith("It looks balanced."),
    );
    expect(await screen.findByText("Copied")).toBeTruthy();
  });
});

describe("permission", () => {
  it("asks what it may use before the first turn, and Not now keeps it usable", async () => {
    vi.mocked(api.getAiConsent).mockResolvedValue(nothingChosen);
    vi.mocked(api.sendAiChatMessage).mockResolvedValue(
      turn("In general, yes."),
    );
    await chat();

    await screen.findByRole("heading", { name: "What can the assistant use?" });
    expect(screen.queryByLabelText("Message the assistant")).toBeNull();

    fireEvent.click(screen.getByRole("button", { name: "Not now" }));

    expect(screen.getByText(NO_ACCESS_LINE)).toBeTruthy();
    fireEvent.change(box(), { target: { value: "is oats ok" } });
    fireEvent.click(screen.getByRole("button", { name: "Send" }));
    await screen.findByText("In general, yes.");
    expect(api.updateAiConsent).not.toHaveBeenCalled();
  });

  it("saves the switches chosen, then opens on the suggestions", async () => {
    vi.mocked(api.getAiConsent).mockResolvedValue(nothingChosen);
    vi.mocked(api.updateAiConsent).mockResolvedValue(
      consent({ shareBody: false }),
    );
    await chat();

    await screen.findByRole("heading", { name: "What can the assistant use?" });
    fireEvent.click(screen.getByRole("switch", { name: /Workouts/ }));
    fireEvent.click(screen.getByRole("button", { name: "Allow and start" }));

    await screen.findByText("What would you like to know?");
    expect(api.updateAiConsent).toHaveBeenCalledWith({
      enabled: true,
      shareNutrition: true,
      shareTraining: false,
      shareBody: false,
      allowWrites: true,
      writeNutrition: true,
      writeTraining: true,
      writeBody: true,
    });
  });

  it("offers to allow just the switch a refusal named, then to send the question again", async () => {
    const reads = consent({
      allowWrites: false,
      writeNutrition: false,
      writeTraining: false,
      writeBody: false,
    });
    vi.mocked(api.getAiConsent).mockResolvedValue(reads);
    vi.mocked(api.sendAiChatMessage).mockResolvedValue({
      ...turn("Logging is switched off.", [], { content: "log eggs" }),
      consentRequired: [
        {
          errorCode: "ai_consent_required",
          axis: "nutrition",
          access: "write",
        },
      ],
    });
    vi.mocked(api.updateAiConsent).mockResolvedValue(
      consent({
        allowWrites: true,
        writeNutrition: true,
        writeTraining: false,
        writeBody: false,
      }),
    );
    await chat();

    fireEvent.change(box(), { target: { value: "log eggs" } });
    fireEvent.click(screen.getByRole("button", { name: "Send" }));

    const needed = await screen.findByRole("group", {
      name: "Permission needed",
    });
    const allowButton = within(needed).getByRole("button", {
      name: "Allow logging meals and plans",
    });
    // The card can render a moment before the send settles and the button is enabled.
    await waitFor(() =>
      expect(allowButton).not.toHaveProperty("disabled", true),
    );
    fireEvent.click(allowButton);
    await waitFor(() =>
      expect(api.updateAiConsent).toHaveBeenCalledWith({
        ...reads,
        updatedAt: undefined,
        allowWrites: true,
        writeNutrition: true,
      }),
    );

    fireEvent.click(
      await within(needed).findByRole("button", { name: "Send again" }),
    );
    await waitFor(() =>
      expect(api.sendAiChatMessage).toHaveBeenLastCalledWith(
        "t1",
        "log eggs",
        expect.any(String),
      ),
    );
  });
});

/** A turn told one event at a time, as a streaming conversation API would. */
function scriptedTurn() {
  const queue: ChatEvent[] = [];
  let wake: (() => void) | null = null;
  let ended = false;
  async function* events(): AsyncGenerator<ChatEvent> {
    for (;;) {
      if (queue.length > 0) {
        yield queue.shift()!;
        continue;
      }
      if (ended) return;
      await new Promise<void>((resolve) => {
        wake = resolve;
      });
      wake = null;
    }
  }
  const transport = {
    send: vi.fn(() => events()),
    confirm: vi.fn().mockResolvedValue(undefined),
    submitToolResult: vi.fn().mockResolvedValue(undefined),
    list: vi.fn().mockResolvedValue([]),
    load: vi.fn(),
    remove: vi.fn().mockResolvedValue(undefined),
  } satisfies ChatTransport;
  return {
    transport,
    async push(...next: ChatEvent[]) {
      await act(async () => {
        queue.push(...next);
        if (next.some((e) => e.type === "turn_finished" || e.type === "error"))
          ended = true;
        wake?.();
      });
    },
  };
}

async function streamedChat(transport: ChatTransport) {
  render(
    <AiChat
      threadId={null}
      onThreadChange={() => {}}
      onClose={close}
      transport={transport}
    />,
  );
  await act(async () => {});
}

function ask(words: string) {
  fireEvent.change(box(), { target: { value: words } });
  fireEvent.click(screen.getByRole("button", { name: "Send" }));
}

describe("a streamed turn", () => {
  it("joins text as it arrives, then keeps the stored reply", async () => {
    const turnOf = scriptedTurn();
    await streamedChat(turnOf.transport);
    ask("ideas for dinner");

    await turnOf.push(
      { type: "turn_started", threadId: null },
      { type: "text", delta: "Try a " },
      { type: "text", delta: "chicken wrap." },
    );
    expect(await screen.findByText("Try a chicken wrap.")).toBeTruthy();
    // Still being written: nothing to copy yet.
    expect(screen.queryByRole("button", { name: "Copy" })).toBeNull();

    await turnOf.push({
      type: "turn_finished",
      threadId: "t5",
      title: "Dinner",
      message: {
        id: "u1",
        fromUser: true,
        content: "ideas for dinner",
        createdAt: "2026-10-01T09:59:00Z",
      },
      reply: reply("Try a chicken wrap.", { id: "r1" }),
    });
    await screen.findByRole("button", { name: "Copy" });
    expect(screen.getAllByText("Try a chicken wrap.")).toHaveLength(1);
    expect(turnOf.transport.send).toHaveBeenCalledWith(
      null,
      { text: "ideas for dinner", image: undefined },
      expect.objectContaining({ idempotencyKey: expect.any(String) }),
    );
  });

  it("shows a confirmation when one is asked, and sends the answer", async () => {
    const turnOf = scriptedTurn();
    await streamedChat(turnOf.transport);
    ask("delete my plan");

    await turnOf.push(
      { type: "turn_started", threadId: "t9" },
      {
        type: "confirmation_requested",
        callId: "c1",
        question: "Delete the plan for next week?",
        fields: [{ label: "Plan", value: "Next week" }],
        expiresAt: null,
      },
    );
    const card = await screen.findByRole("group", {
      name: "Confirm before it acts",
    });
    expect(
      within(card).getByText("Delete the plan for next week?"),
    ).toBeTruthy();
    expect(within(card).getByText("Next week")).toBeTruthy();

    fireEvent.click(within(card).getByRole("button", { name: "Approve" }));
    await within(card).findByText("Approved.");
    expect(turnOf.transport.confirm).toHaveBeenCalledWith("t9", "c1", true);
  });

  it("drops a blocked message and gives the words back, with no hint to retry", async () => {
    const turnOf = scriptedTurn();
    await streamedChat(turnOf.transport);
    ask("something it will not do");

    await turnOf.push(
      { type: "turn_started", threadId: null },
      {
        type: "error",
        kind: "blocked",
        code: "blocked",
        message: "Policy refusal.",
        retryAfter: 60,
      },
    );
    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toBe("I can't help with that.");
    expect(screen.queryByRole("article", { name: "You said" })).toBeNull();
    expect(box().value).toBe("something it will not do");
  });

  it("says once when to try again after too many messages", async () => {
    const turnOf = scriptedTurn();
    await streamedChat(turnOf.transport);
    ask("one more");

    await turnOf.push({
      type: "error",
      kind: "rate_limited",
      code: "ai_rate_limited",
      message: "The assistant is busy right now. Try again in 30 seconds.",
      retryAfter: 30,
    });
    expect((await screen.findByRole("alert")).textContent).toBe(
      "The assistant is busy right now. Try again in 30 seconds.",
    );
    expect(box().value).toBe("one more");
  });
});
