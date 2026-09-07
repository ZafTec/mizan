import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import { ApiError } from "@/lib/api";
import { clientApi } from "@/lib/api.client";
import MealLogForm from "./MealLogForm";
import MeasurementLogForm from "./MeasurementLogForm";
import SaveMealAsRecipe from "./SaveMealAsRecipe";

const { refresh } = vi.hoisted(() => ({ refresh: vi.fn() }));
vi.mock("next/navigation", () => ({ useRouter: () => ({ refresh }) }));
vi.mock("@/lib/api.client", () => ({ clientApi: vi.fn() }));
vi.mock("@/lib/toast", () => ({
  appToast: { success: vi.fn() },
  getErrorMessage: (error: unknown, fallback: string) =>
    error instanceof Error ? error.message : fallback,
}));

const rice = {
  id: "rice",
  name: "Brown rice",
  servingSize: 100,
  servingUnit: "g",
  caloriesPer100g: 120,
  proteinPer100g: 3,
};
const bowl = {
  id: "bowl",
  title: "Chicken bowl",
  nutrition: { caloriesPerServing: 400, proteinGrams: 30 },
};

function searchResponse(path: string) {
  if (path.startsWith("/api/Foods/search")) return { items: [rice] };
  if (path.includes("FavoritesOnly")) return { items: [bowl] };
  if (path.startsWith("/api/Recipes?")) return { items: [bowl] };
  if (path.startsWith("/api/Meals?")) return { entries: [] };
  return { id: "saved" };
}

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(clientApi).mockImplementation(
    async (path) => searchResponse(path) as never,
  );
});
afterEach(cleanup);

describe("in-place logging", () => {
  it("logs foods by grams and recipes by servings on the chosen day", async () => {
    const onSaved = vi.fn();
    render(
      <MealLogForm
        initialDate="2026-09-05"
        initialMealType="DINNER"
        onSaved={onSaved}
        onBusyChange={vi.fn()}
      />,
    );
    fireEvent.click(
      await screen.findByRole("button", { name: "Add Brown rice" }),
    );
    fireEvent.click(screen.getByRole("button", { name: "Add Chicken bowl" }));
    fireEvent.change(screen.getByLabelText("Brown rice quantity in grams"), {
      target: { value: "150" },
    });
    fireEvent.change(
      screen.getByLabelText("Chicken bowl quantity in servings"),
      { target: { value: "0.5" } },
    );
    fireEvent.click(screen.getByRole("button", { name: "Log meal" }));
    await waitFor(() => expect(onSaved).toHaveBeenCalledWith("Dinner logged"));
    const writes = vi
      .mocked(clientApi)
      .mock.calls.filter(([, options]) => options?.method === "POST");
    expect(writes).toEqual([
      [
        "/api/Nutrition/log",
        {
          method: "POST",
          body: {
            foodId: "rice",
            entryDate: "2026-09-05",
            mealType: "DINNER",
            servings: 1.5,
          },
        },
      ],
      [
        "/api/Nutrition/log",
        {
          method: "POST",
          body: {
            recipeId: "bowl",
            entryDate: "2026-09-05",
            mealType: "DINNER",
            servings: 0.5,
          },
        },
      ],
    ]);
  });

  it("retains only unsaved items after a partial failure so retry cannot duplicate entries", async () => {
    let failRecipe = true;
    vi.mocked(clientApi).mockImplementation(async (path, options) => {
      if (
        options?.method === "POST" &&
        (options.body as { recipeId?: string })?.recipeId &&
        failRecipe
      )
        throw new Error("Connection lost. Please try again.");
      return searchResponse(path) as never;
    });
    const onSaved = vi.fn();
    render(
      <MealLogForm
        initialDate="2026-09-05"
        onSaved={onSaved}
        onBusyChange={vi.fn()}
      />,
    );
    fireEvent.click(
      await screen.findByRole("button", { name: "Add Brown rice" }),
    );
    fireEvent.click(screen.getByRole("button", { name: "Add Chicken bowl" }));
    fireEvent.click(screen.getByRole("button", { name: "Log meal" }));
    expect((await screen.findByRole("alert")).textContent).toContain(
      "1 item was saved",
    );
    const cart = screen.getByRole("region", { name: "Items in this meal" });
    expect(within(cart).queryByText("Brown rice")).toBeNull();
    expect(within(cart).getByText("Chicken bowl")).toBeTruthy();
    expect(refresh).toHaveBeenCalledOnce();
    failRecipe = false;
    fireEvent.click(screen.getByRole("button", { name: "Log meal" }));
    await waitFor(() => expect(onSaved).toHaveBeenCalledOnce());
    const foodWrites = vi
      .mocked(clientApi)
      .mock.calls.filter(
        ([, options]) =>
          options?.method === "POST" &&
          (options.body as { foodId?: string })?.foodId,
      );
    expect(foodWrites).toHaveLength(1);
  });

  it("rejects an empty measurement and saves an entered weight without replacing the date", async () => {
    const onSaved = vi.fn();
    render(
      <MeasurementLogForm
        initialDate="2026-09-05"
        onSaved={onSaved}
        onBusyChange={vi.fn()}
      />,
    );
    fireEvent.click(screen.getByRole("button", { name: "Log measurement" }));
    expect(screen.getByRole("alert").textContent).toContain(
      "at least one body measurement",
    );
    expect(clientApi).not.toHaveBeenCalled();
    fireEvent.change(screen.getByLabelText("Weight (kg)"), {
      target: { value: "74.3" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Log measurement" }));
    await waitFor(() =>
      expect(onSaved).toHaveBeenCalledWith("Measurement logged"),
    );
    expect(clientApi).toHaveBeenCalledWith("/api/BodyMeasurements", {
      method: "POST",
      body: expect.objectContaining({
        date: "2026-09-05",
        weightKg: 74.3,
        bodyFatPercentage: null,
        notes: null,
      }),
    });
  });

  it("asks for missing promotion weights and retries the same named meal with explicit grams", async () => {
    let promotionAttempts = 0;
    vi.mocked(clientApi).mockImplementation(async (path) => {
      if (path === "/api/Recipes/promote" && promotionAttempts++ === 0) {
        throw new ApiError(400, "Bad Request", {
          errorCode: "promotion_weights_required",
          items: [
            { id: "recipe-1", name: "Homemade sauce", kind: "recipe" },
            { id: "entry-1", name: "Bread", kind: "entry" },
          ],
        });
      }
      return { recipeId: "new-recipe" } as never;
    });
    render(
      <SaveMealAsRecipe date="2026-09-05" mealType="LUNCH" itemCount={2} />,
    );
    fireEvent.click(screen.getByRole("button", { name: "Save as recipe" }));
    fireEvent.change(screen.getByLabelText("Recipe name"), {
      target: { value: "Lunch sandwich" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Save recipe" }));
    fireEvent.change(await screen.findByLabelText("Homemade sauce (g)"), {
      target: { value: "300" },
    });
    fireEvent.change(screen.getByLabelText("Bread (g)"), {
      target: { value: "80" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Save recipe" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(clientApi).toHaveBeenLastCalledWith("/api/Recipes/promote", {
      method: "POST",
      body: {
        entryDate: "2026-09-05",
        mealType: "LUNCH",
        title: "Lunch sandwich",
        recipeYields: { "recipe-1": 300 },
        entryWeightsGrams: { "entry-1": 80 },
      },
    });
  });
});
