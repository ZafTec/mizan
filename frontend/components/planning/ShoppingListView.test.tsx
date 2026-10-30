import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { clientApi } from "@/lib/api.client";
import ShoppingListView, { groupItems } from "./ShoppingListView";
import type {
  ShoppingListDetail,
  ShoppingListItem,
} from "@/lib/api/shoppingLists";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ refresh: vi.fn(), push: vi.fn() }),
}));
vi.mock("@/lib/api.client", () => ({ clientApi: vi.fn() }));
vi.mock("@/lib/toast", () => ({
  appToast: { success: vi.fn(), error: vi.fn() },
  getErrorMessage: (error: unknown, fallback: string) =>
    error instanceof Error ? error.message : fallback,
}));

const item = (overrides: Partial<ShoppingListItem>): ShoppingListItem => ({
  id: "item",
  itemName: "Tomatoes",
  needsReview: false,
  sortOrder: 0,
  isChecked: false,
  ...overrides,
});

const list: ShoppingListDetail = {
  id: "list-1",
  name: "Weekly shop",
  userId: "user",
  totalItems: 2,
  checkedItems: 0,
  needsReviewCount: 1,
  canEdit: true,
  currency: "ETB",
  costCoverage: 0,
  unpricedCount: 0,
  createdAt: "2026-10-20T08:00:00Z",
  updatedAt: "2026-10-20T08:00:00Z",
  items: [
    item({ id: "tomatoes", itemName: "Tomatoes", grams: 500 }),
    item({
      id: "salt",
      itemName: "Salt",
      amount: 1,
      unit: "pinch",
      needsReview: true,
      sortOrder: 1,
    }),
  ],
};

beforeEach(() => {
  vi.clearAllMocks();
});
afterEach(cleanup);

describe("ticking an item", () => {
  it("ticks at once and keeps the tick when the API agrees", async () => {
    let resolve: () => void = () => {};
    vi.mocked(clientApi).mockImplementation(
      () =>
        new Promise<never>((done) => {
          resolve = () => done(undefined as never);
        }),
    );
    render(<ShoppingListView list={list} />);
    const box = screen.getByRole("checkbox", {
      name: /Tomatoes/,
    }) as HTMLInputElement;
    expect(box.checked).toBe(false);
    fireEvent.click(box);
    expect(box.checked).toBe(true);
    expect(screen.getByTestId("items-left").textContent).toBe("1 left of 2");
    const [path, options] = vi.mocked(clientApi).mock.calls[0];
    expect(path).toBe("/api/ShoppingLists/items/tomatoes/toggle");
    expect(options).toMatchObject({
      method: "PATCH",
      body: { isChecked: true },
    });
    expect(options?.headers?.["Idempotency-Key"]).toMatch(/[0-9a-f-]{36}/);
    resolve();
    await waitFor(() => expect(box.checked).toBe(true));
  });

  it("reverts the tick when the API refuses", async () => {
    vi.mocked(clientApi).mockRejectedValue(new Error("offline"));
    render(<ShoppingListView list={list} />);
    const box = screen.getByRole("checkbox", {
      name: /Salt/,
    }) as HTMLInputElement;
    fireEvent.click(box);
    expect(box.checked).toBe(true);
    await waitFor(() => expect(box.checked).toBe(false));
    expect(screen.getByTestId("items-left").textContent).toBe("2 left of 2");
  });

  it("shows weights as grams and marks lines to check", () => {
    render(<ShoppingListView list={list} />);
    expect(screen.getByText("500 g")).toBeTruthy();
    expect(screen.getByText("1 pinch")).toBeTruthy();
    expect(screen.getByText("Check")).toBeTruthy();
  });
});

describe("grouping items", () => {
  it("groups by cooking session when any item has one, else by category", () => {
    const bySession = groupItems([
      item({ id: "a", prepSessionId: "s1", prepSessionTitle: "Sunday batch" }),
      item({ id: "b", category: "Produce" }),
    ]);
    expect(bySession.map((group) => group.title)).toEqual([
      "Sunday batch",
      "Any time",
    ]);
    const byCategory = groupItems([
      item({ id: "a", category: "Produce", sortOrder: 1 }),
      item({ id: "b", sortOrder: 0 }),
    ]);
    expect(byCategory.map((group) => group.title)).toEqual([
      "Other",
      "Produce",
    ]);
  });
});
