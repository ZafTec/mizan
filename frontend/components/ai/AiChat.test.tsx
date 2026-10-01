import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import AiChat from "./AiChat";
import * as api from "@/lib/api/ai";

const { refresh } = vi.hoisted(() => ({ refresh: vi.fn() }));
vi.mock("next/navigation", () => ({ useRouter: () => ({ refresh }) }));
vi.mock("@/lib/api/ai", () => ({
  listAiChatThreads: vi.fn(),
  getAiChatThread: vi.fn(),
  deleteAiChatThread: vi.fn(),
  sendAiChatMessage: vi.fn(),
  sendAiChatImage: vi.fn(),
}));

const prompts = [{ id: "p1", label: "Ideas for today", prompt: "What fits my macros?", icon: "flame" as const }];
const reply = (content: string, extra: object = {}) => ({
  id: crypto.randomUUID(),
  fromUser: false,
  content,
  createdAt: "2026-10-01T10:00:00Z",
  ...extra,
});
const turn = (content: string, performed: api.AiToolInvocation[] = []) => ({
  threadId: "t1",
  title: "Macros",
  reply: reply(content),
  performed,
});

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(api.listAiChatThreads).mockResolvedValue([
    { id: "t0", title: "Last week", updatedAt: "2026-09-24T10:00:00Z" },
  ]);
  vi.mocked(api.getAiChatThread).mockResolvedValue({
    id: "t0",
    title: "Last week",
    updatedAt: "2026-09-24T10:00:00Z",
    messages: [
      { id: "m1", fromUser: true, content: "Is this ok?", createdAt: "2026-09-24T10:00:00Z", imageUrl: "https://cdn.test/plate.jpg" },
      reply("It looks balanced.", { id: "m2" }),
    ],
  });
  vi.stubGlobal("URL", Object.assign(URL, { createObjectURL: vi.fn(() => "blob:preview"), revokeObjectURL: vi.fn() }));
  Element.prototype.scrollTo = vi.fn();
});
afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

const box = () => screen.getByLabelText("Message the assistant") as HTMLTextAreaElement;

describe("the assistant conversation", () => {
  it("opens on suggestions, and a suggestion sends as a turn", async () => {
    vi.mocked(api.sendAiChatMessage).mockResolvedValue(turn("Try a chicken wrap."));
    render(<AiChat quickPrompts={prompts} />);

    fireEvent.click(screen.getByRole("button", { name: /Ideas for today/ }));

    await screen.findByText("Try a chicken wrap.");
    expect(api.sendAiChatMessage).toHaveBeenCalledWith(null, "What fits my macros?");
    const log = screen.getByRole("log", { name: "Conversation" });
    expect(within(log).getByRole("article", { name: "You said" })).toBeTruthy();
    expect(within(log).getByRole("article", { name: "The assistant replied" })).toBeTruthy();
  });

  it("sends on Enter, keeps a new line on Shift and Enter, and ignores an empty box", async () => {
    vi.mocked(api.sendAiChatMessage).mockResolvedValue(turn("Sure."));
    render(<AiChat quickPrompts={prompts} />);

    fireEvent.keyDown(box(), { key: "Enter" });
    expect(api.sendAiChatMessage).not.toHaveBeenCalled();

    fireEvent.change(box(), { target: { value: "hello" } });
    fireEvent.keyDown(box(), { key: "Enter", shiftKey: true });
    expect(api.sendAiChatMessage).not.toHaveBeenCalled();

    fireEvent.keyDown(box(), { key: "Enter" });
    await waitFor(() => expect(api.sendAiChatMessage).toHaveBeenCalledWith(null, "hello"));
    await screen.findByText("Sure.");
    expect(box().value).toBe("");
  });

  it("gives the person their words back when a turn fails, and says why", async () => {
    vi.mocked(api.sendAiChatMessage).mockRejectedValue(new Error("You have used today's assistant allowance."));
    render(<AiChat quickPrompts={prompts} />);

    fireEvent.change(box(), { target: { value: "a long question" } });
    fireEvent.click(screen.getByRole("button", { name: "Send" }));

    expect((await screen.findByRole("alert")).textContent).toContain("allowance");
    expect(box().value).toBe("a long question");
    expect(screen.queryByRole("article", { name: "You said" })).toBeNull();
  });

  it("shows what a reply did, and refreshes the page behind it", async () => {
    vi.mocked(api.sendAiChatMessage).mockResolvedValue(
      turn("Logged it.", [{ tool: "log_meal", summary: "Logged Greek yogurt (150 kcal)", succeeded: true }]),
    );
    render(<AiChat quickPrompts={prompts} />);

    fireEvent.change(box(), { target: { value: "log yogurt" } });
    fireEvent.click(screen.getByRole("button", { name: "Send" }));

    await screen.findByText("Logged Greek yogurt (150 kcal)");
    expect(refresh).toHaveBeenCalled();
  });

  it("renders the assistant's markdown and the person's own text literally", async () => {
    vi.mocked(api.sendAiChatMessage).mockResolvedValue(turn("You are **on track**."));
    render(<AiChat quickPrompts={prompts} />);

    fireEvent.change(box(), { target: { value: "is *this* fine" } });
    fireEvent.click(screen.getByRole("button", { name: "Send" }));

    expect((await screen.findByText("on track")).tagName).toBe("STRONG");
    expect(screen.getByText("is *this* fine")).toBeTruthy();
  });
});

describe("photos", () => {
  const photo = () => new File([new Uint8Array([0xff, 0xd8, 0xff])], "lunch.jpg", { type: "image/jpeg" });

  it("shows a thumbnail before sending, which can be previewed full size or removed", () => {
    render(<AiChat quickPrompts={prompts} />);

    fireEvent.change(screen.getByLabelText("Choose a photo"), { target: { files: [photo()] } });

    expect(screen.getByText("lunch.jpg")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Preview the photo to send" }));
    expect(screen.getByRole("dialog")).toBeTruthy();
    expect(within(screen.getByRole("dialog")).getByRole("img", { name: "Photo to send" })).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Close photo" }));
    expect(screen.queryByRole("dialog")).toBeNull();

    fireEvent.click(screen.getByRole("button", { name: "Remove photo" }));
    expect(screen.queryByText("lunch.jpg")).toBeNull();
  });

  it("sends a photo on its own, with no words", async () => {
    vi.mocked(api.sendAiChatImage).mockResolvedValue(turn("That looks like rice and beans."));
    render(<AiChat quickPrompts={prompts} />);

    fireEvent.change(screen.getByLabelText("Choose a photo"), { target: { files: [photo()] } });
    fireEvent.click(screen.getByRole("button", { name: "Send" }));

    await screen.findByText("That looks like rice and beans.");
    expect(api.sendAiChatImage).toHaveBeenCalledWith(null, "", expect.any(File));
  });

  it("opens a photo in a past conversation full size, and closes it with Escape", async () => {
    render(<AiChat quickPrompts={prompts} />);
    fireEvent.click(await screen.findByRole("button", { name: "Last week" }));
    await screen.findByText("It looks balanced.");

    fireEvent.click(screen.getByRole("button", { name: "Open photo" }));

    const dialog = screen.getByRole("dialog");
    expect((within(dialog).getByRole("img", { name: "Photo you sent" }) as HTMLImageElement).src).toBe("https://cdn.test/plate.jpg");
    fireEvent.keyDown(dialog, { key: "Escape" });
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
  });
});

describe("conversations", () => {
  it("lists earlier ones, opens one, and starts a new one", async () => {
    render(<AiChat quickPrompts={prompts} />);

    fireEvent.click((await screen.findAllByRole("button", { name: "Last week" }))[0]);
    await screen.findByText("It looks balanced.");
    expect(api.getAiChatThread).toHaveBeenCalledWith("t0");

    fireEvent.click(screen.getAllByRole("button", { name: "New conversation" })[0]);
    expect(screen.queryByText("It looks balanced.")).toBeNull();
    expect(screen.getByText("What would you like to know?")).toBeTruthy();
  });

  it("asks before deleting a conversation, and only deletes when confirmed", async () => {
    vi.mocked(api.deleteAiChatThread).mockResolvedValue(undefined);
    render(<AiChat quickPrompts={prompts} />);

    fireEvent.click((await screen.findAllByRole("button", { name: "Delete Last week" }))[0]);
    expect(api.deleteAiChatThread).not.toHaveBeenCalled();
    fireEvent.click(await screen.findByRole("button", { name: "Delete" }));

    await waitFor(() => expect(api.deleteAiChatThread).toHaveBeenCalledWith("t0"));
    await waitFor(() => expect(screen.queryAllByRole("button", { name: "Delete Last week" })).toHaveLength(0));
  });

  it("copies a reply", async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.assign(navigator, { clipboard: { writeText } });
    render(<AiChat quickPrompts={prompts} />);
    fireEvent.click((await screen.findAllByRole("button", { name: "Last week" }))[0]);
    await screen.findByText("It looks balanced.");

    fireEvent.click(screen.getByRole("button", { name: /Copy/ }));

    await waitFor(() => expect(writeText).toHaveBeenCalledWith("It looks balanced."));
    expect(await screen.findByText("Copied")).toBeTruthy();
  });
});
