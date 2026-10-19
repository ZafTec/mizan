import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import { clientApi } from "@/lib/api.client";
import MealLogForm from "./MealLogForm";

const { refresh } = vi.hoisted(() => ({ refresh: vi.fn() }));
vi.mock("next/navigation", () => ({ useRouter: () => ({ refresh }) }));
vi.mock("@/lib/api.client", () => ({ clientApi: vi.fn() }));
vi.mock("@/lib/toast", () => ({
  appToast: { success: vi.fn(), error: vi.fn() },
  getErrorMessage: (error: unknown, fallback: string) =>
    error instanceof Error ? error.message : fallback,
}));

const analysis = {
  foods: [
    { name: "Scrambled eggs", portionGrams: 100, calories: 150, protein: 10, carbs: 2, fat: 11 },
    { name: "Toast", portionGrams: 50, calories: 130, protein: 4, carbs: 24, fat: 2 },
  ],
  totalCalories: 280,
  confidence: 0.7,
  note: "Assumed one slice.",
};

const recipe = (id: string, title: string, extra: object = {}) => ({
  id,
  title,
  nutrition: { caloriesPerServing: 400, proteinGrams: 30 },
  ...extra,
});

let pro = true;
let analyzed: unknown = analysis;

function respond(path: string, options?: { method?: string; body?: unknown }) {
  if (path === "/api/Subscriptions/me") return { isPro: pro };
  if (path === "/api/Nutrition/ai/analyze-text") {
    if (analyzed instanceof Error) throw analyzed;
    return analyzed;
  }
  if (path.includes("FavoritesOnly")) return { items: [recipe("fav", "Favorite curry"), recipe("both", "Both bowl")] };
  if (path.includes("SortBy=frequent"))
    return { items: [recipe("both", "Both bowl", { timesLogged: 9 }), recipe("often", "Often wrap", { timesLogged: 4 })] };
  if (path.startsWith("/api/Recipes?"))
    return { items: [recipe("fav", "Favorite curry"), recipe("often", "Often wrap"), recipe("other", "Other soup")] };
  if (path.startsWith("/api/Foods/search")) return { items: [] };
  if (path.startsWith("/api/Meals?")) return { entries: [] };
  if (options?.method === "POST") return { id: "saved" };
  return {};
}

beforeEach(() => {
  vi.clearAllMocks();
  pro = true;
  analyzed = analysis;
  vi.mocked(clientApi).mockImplementation(async (path, options) => respond(path, options as never) as never);
});
afterEach(cleanup);

function renderForm(onSaved = vi.fn()) {
  render(<MealLogForm initialDate="2026-09-05" initialMealType="BREAKFAST" onSaved={onSaved} onBusyChange={vi.fn()} />);
  return onSaved;
}

async function estimate(text = "two eggs and toast") {
  fireEvent.change(screen.getByLabelText("Tell me what you ate"), { target: { value: text } });
  fireEvent.click(await screen.findByRole("button", { name: "Estimate meal" }));
}


describe("describing a meal to the assistant", () => {
  it("leads with the description box, and search is a step away", () => {
    renderForm();

    expect(screen.getByRole("region", { name: "Describe your meal" })).toBeTruthy();
    expect(screen.queryByRole("search")).toBeNull();
    expect(screen.getByRole("button", { name: "Search foods" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Log a recipe" })).toBeTruthy();
  });

  it("will not estimate an empty description", () => {
    renderForm();
    const button = screen.getByRole("button", { name: "Estimate meal" }) as HTMLButtonElement;
    expect(button.disabled).toBe(true);
  });

  it("turns a sentence into editable items and logs nothing yet", async () => {
    renderForm();

    await estimate();

    const meal = await screen.findByRole("region", { name: "Items in this meal" });
    expect(within(meal).getByDisplayValue("Scrambled eggs")).toBeTruthy();
    expect(within(meal).getByDisplayValue("Toast")).toBeTruthy();
    expect(meal.textContent).toContain("Estimated from your description at 70% confidence");
    expect(meal.textContent).toContain("Assumed one slice.");
    expect(vi.mocked(clientApi)).toHaveBeenCalledWith("/api/Nutrition/ai/analyze-text", {
      method: "POST",
      body: { description: "two eggs and toast" },
    });
    expect(vi.mocked(clientApi).mock.calls.some(([path]) => path === "/api/Meals")).toBe(false);
    expect((screen.getByLabelText("Tell me what you ate") as HTMLTextAreaElement).value).toBe("");
  });

  it("scales the numbers when the weight changes, and logs what the person confirmed", async () => {
    const onSaved = renderForm();
    await estimate();
    const meal = await screen.findByRole("region", { name: "Items in this meal" });

    fireEvent.change(within(meal).getByLabelText("Scrambled eggs quantity in grams"), { target: { value: "200" } });
    fireEvent.change(within(meal).getAllByLabelText("Food name")[1], { target: { value: "Sourdough toast" } });
    fireEvent.click(within(meal).getByRole("button", { name: "Remove Sourdough toast" }));
    fireEvent.click(screen.getByRole("button", { name: "Log meal" }));

    await waitFor(() => expect(onSaved).toHaveBeenCalledWith("Breakfast logged"));
    const posts = vi.mocked(clientApi).mock.calls.filter(([path]) => path === "/api/Meals");
    expect(posts).toHaveLength(1);
    expect(posts[0][1]).toEqual({
      method: "POST",
      body: {
        name: "Scrambled eggs",
        entryDate: "2026-09-05",
        mealType: "BREAKFAST",
        servings: 1,
        amountGrams: 200,
        calories: 300,
        proteinGrams: 20,
        carbsGrams: 4,
        fatGrams: 22,
      },
    });
  });

  it("keeps only the unsaved items after a failure, so a retry cannot log anything twice", async () => {
    let failSecond = true;
    vi.mocked(clientApi).mockImplementation(async (path, options) => {
      const body = (options as { body?: { name?: string } } | undefined)?.body;
      if (path === "/api/Meals" && body?.name === "Toast" && failSecond) throw new Error("Connection lost.");
      return respond(path, options as never) as never;
    });
    const onSaved = renderForm();
    await estimate();

    fireEvent.click(await screen.findByRole("button", { name: "Log meal" }));

    expect((await screen.findByRole("alert")).textContent).toContain("1 item was saved");
    const meal = screen.getByRole("region", { name: "Items in this meal" });
    expect(within(meal).queryByDisplayValue("Scrambled eggs")).toBeNull();
    expect(within(meal).getByDisplayValue("Toast")).toBeTruthy();

    failSecond = false;
    fireEvent.click(screen.getByRole("button", { name: "Log meal" }));
    await waitFor(() => expect(onSaved).toHaveBeenCalledOnce());
    const eggs = vi.mocked(clientApi).mock.calls.filter(([, o]) => (o as { body?: { name?: string } })?.body?.name === "Scrambled eggs");
    expect(eggs).toHaveLength(1);
  });

  it("says so when it found no food, and leaves the meal alone", async () => {
    analyzed = { foods: [], totalCalories: 0, confidence: 0, note: null };
    renderForm();

    await estimate("hmm");

    expect((await screen.findByRole("alert")).textContent).toContain("could not find any food");
    expect(screen.queryByRole("region", { name: "Items in this meal" })).toBeNull();
  });

  it("shows the assistant's own error and keeps what the person typed", async () => {
    analyzed = new Error("You have used today's assistant allowance.");
    renderForm();

    await estimate("a big dinner");

    expect((await screen.findByRole("alert")).textContent).toContain("allowance");
    expect((screen.getByLabelText("Tell me what you ate") as HTMLTextAreaElement).value).toBe("a big dinner");
  });

  it("walls a free account at the moment it tries, without calling the assistant", async () => {
    pro = false;
    renderForm();
    await waitFor(() => expect(vi.mocked(clientApi)).toHaveBeenCalledWith("/api/Subscriptions/me"));
    await screen.findByRole("button", { name: "Estimate meal" });
    fireEvent.change(screen.getByLabelText("Tell me what you ate"), { target: { value: "two eggs" } });
    await waitFor(() => expect((screen.getByRole("button", { name: "Estimate meal" }) as HTMLButtonElement).disabled).toBe(false));
    // Let the subscription answer land so the wall knows the account is free.
    await new Promise((resolve) => setTimeout(resolve, 0));

    fireEvent.click(screen.getByRole("button", { name: "Estimate meal" }));

    expect(await screen.findByText("Log a meal in plain words")).toBeTruthy();
    expect(vi.mocked(clientApi).mock.calls.some(([path]) => path === "/api/Nutrition/ai/analyze-text")).toBe(false);
  });

  it("estimates a photo through the same review", async () => {
    const fetchMock = vi.fn(async () => new Response(JSON.stringify(analysis), { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);
    renderForm();
    await waitFor(() => expect(vi.mocked(clientApi)).toHaveBeenCalledWith("/api/Subscriptions/me"));

    const file = new File([new Uint8Array([0xff, 0xd8, 0xff])], "plate.jpg", { type: "image/jpeg" });
    fireEvent.change(screen.getByLabelText("Meal photo"), { target: { files: [file] } });

    const meal = await screen.findByRole("region", { name: "Items in this meal" });
    expect(meal.textContent).toContain("your photo");
    expect(fetchMock).toHaveBeenCalledOnce();
    vi.unstubAllGlobals();
  });
});

describe("logging from a recipe", () => {
  async function openRecipes() {
    fireEvent.click(screen.getByRole("button", { name: "Log a recipe" }));
    return screen.findByRole("region", { name: "Log from a recipe" });
  }

  it("swaps to its own view with favorites first, then often logged, then the rest, each recipe once", async () => {
    renderForm();

    const view = await openRecipes();

    const groups = await within(view).findAllByRole("group");
    expect(groups.map((g) => g.getAttribute("aria-label"))).toEqual(["Favorites", "Often logged", "All recipes"]);
    const titles = (group: HTMLElement) => within(group).getAllByRole("button").map((b) => b.getAttribute("aria-label"));
    expect(titles(groups[0])).toEqual(["Add Favorite curry", "Add Both bowl"]);
    expect(titles(groups[1])).toEqual(["Add Often wrap"]);
    expect(titles(groups[2])).toEqual(["Add Other soup"]);
    expect(within(groups[1]).getByText(/Logged 4 times/)).toBeTruthy();
  });

  it("goes back to the meal with the recipe added, and keeps what was already there", async () => {
    renderForm();
    await estimate();
    await screen.findByRole("region", { name: "Items in this meal" });

    await openRecipes();
    fireEvent.click(await screen.findByRole("button", { name: "Add Often wrap" }));

    expect(screen.queryByRole("region", { name: "Log from a recipe" })).toBeNull();
    expect((await screen.findByRole("status")).textContent).toBe("Added Often wrap");
    const meal = screen.getByRole("region", { name: "Items in this meal" });
    expect(within(meal).getByText("Often wrap")).toBeTruthy();
    expect(within(meal).getByDisplayValue("Scrambled eggs")).toBeTruthy();
  });

  it("filters the whole view as the person types", async () => {
    renderForm();
    const view = await openRecipes();
    await within(view).findAllByRole("group");

    fireEvent.change(within(view).getByLabelText("Search recipes"), { target: { value: "soup" } });

    await waitFor(() => expect(within(view).queryByRole("group", { name: "Favorites" })).toBeNull());
    expect(within(view).getByRole("button", { name: "Add Other soup" })).toBeTruthy();
    expect(within(view).queryByRole("button", { name: "Add Favorite curry" })).toBeNull();
  });

  it("offers a way back that changes nothing", async () => {
    renderForm();
    const view = await openRecipes();

    fireEvent.click(within(view).getByRole("button", { name: "Back to your meal" }));

    expect(screen.queryByRole("region", { name: "Log from a recipe" })).toBeNull();
    expect(screen.getByRole("region", { name: "Describe your meal" })).toBeTruthy();
  });

  it("says when there are no recipes, and lets the person retry when they cannot load", async () => {
    vi.mocked(clientApi).mockImplementation(async (path) => {
      if (path.startsWith("/api/Recipes")) throw new Error("down");
      return respond(path) as never;
    });
    renderForm();
    const view = await openRecipes();

    expect((await within(view).findByRole("alert")).textContent).toContain("could not load");
    expect(within(view).getByRole("button", { name: "Try again" })).toBeTruthy();
  });
});
