import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import type { User } from "@/lib/auth";
import { LogEntryButton } from "@/components/logging/LogEntryButton";
import AppShell from "./AppShell";

const route = vi.hoisted(() => ({
  pathname: "/today",
  query: "date=2026-09-05",
}));
vi.mock("next/navigation", () => ({
  usePathname: () => route.pathname,
  useSearchParams: () => new URLSearchParams(route.query),
  useRouter: () => ({ refresh: vi.fn(), replace: vi.fn(), push: vi.fn() }),
}));
vi.mock("next/image", () => ({ default: () => null }));
vi.mock("@/lib/auth-client", () => ({
  signOut: vi.fn(),
  useSession: () => ({ data: { user } }),
}));
vi.mock("@/lib/api.client", () => ({ clientApi: vi.fn() }));
vi.mock("@/components/NotificationBell", () => ({
  NotificationBell: () => null,
}));
vi.mock("@/components/gamification/GamificationToaster", () => ({
  GamificationToaster: () => null,
}));
vi.mock("./HouseholdSwitcher", () => ({ default: () => null }));
vi.mock("@/components/ai/FoodPhotoSheet", () => ({ default: () => null }));
vi.mock("@/components/logging/FoodPicker", () => ({ default: () => null }));

const user: User = {
  id: "test-user",
  email: "test@example.com",
  name: "Test User",
  role: "user",
  emailVerified: true,
  themePreference: "system",
  compactMode: false,
  reduceAnimations: false,
  hasPassword: true,
  timeZoneId: "America/Los_Angeles",
};

beforeEach(() => {
  vi.clearAllMocks();
  vi.useFakeTimers({ toFake: ["Date"] });
  // The user's current day is September 6 even though UTC is September 7.
  vi.setSystemTime(new Date("2026-09-07T01:00:00Z"));
  route.pathname = "/today";
  route.query = "date=2026-09-05";
});
afterEach(() => {
  cleanup();
  vi.useRealTimers();
});

function openGlobalLog(buttonIndex = 0) {
  fireEvent.click(
    screen.getAllByRole("button", { name: "Log an entry" })[buttonIndex],
  );
}

function choose(kind: "Meal" | "Measurement" | "Workout") {
  fireEvent.click(screen.getByRole("button", { name: new RegExp(`^${kind}`) }));
}

function expectDate(date: string) {
  expect((screen.getByLabelText("Date") as HTMLInputElement).value).toBe(date);
}

describe("global logging date context", () => {
  it.each([
    ["desktop", 0],
    ["mobile", 1],
  ] as const)(
    "keeps the historical Today date for meals, measurements, and workouts from %s",
    (_location, buttonIndex) => {
      render(<AppShell user={user}>Today</AppShell>);

      for (const kind of ["Meal", "Measurement", "Workout"] as const) {
        openGlobalLog(buttonIndex);
        choose(kind);
        if (kind === "Workout") {
          expect(
            screen
              .getByRole("link", { name: "Open workout" })
              .getAttribute("href"),
          ).toBe("/workout/active?date=2026-09-05");
        } else {
          expectDate("2026-09-05");
        }
        fireEvent.click(screen.getByRole("button", { name: "Close logging" }));
      }
    },
  );

  it.each(["", "date=invalid", "date=2026-02-30", "date=2026-09-07"])(
    "uses the user's current day for an absent, invalid, or future Today date (%s)",
    (query) => {
      route.query = query;
      render(<AppShell user={user}>Today</AppShell>);
      openGlobalLog();
      choose("Measurement");
      expectDate("2026-09-06");
    },
  );

  it("does not treat a History range or date query as a logging day", () => {
    route.pathname = "/history";
    route.query = "date=2026-09-05&from=2026-09-01&to=2026-09-05";
    render(<AppShell user={user}>History</AppShell>);
    openGlobalLog();
    choose("Workout");
    expect(
      screen.getByRole("link", { name: "Open workout" }).getAttribute("href"),
    ).toBe("/workout/active?date=2026-09-06");
  });

  it("preserves an explicit event date, meal type, and recipe, then resets the next global entry", () => {
    render(
      <AppShell user={user}>
        <LogEntryButton
          kind="meal"
          date="2026-09-01"
          mealType="LUNCH"
          recipe={{
            id: "recipe-1",
            title: "Chicken bowl",
            nutrition: { caloriesPerServing: 400, proteinGrams: 30 },
          }}
        >
          Log this recipe
        </LogEntryButton>
      </AppShell>,
    );
    fireEvent.click(screen.getByRole("button", { name: "Log this recipe" }));
    expectDate("2026-09-01");
    expect((screen.getByLabelText("Meal") as HTMLSelectElement).value).toBe(
      "LUNCH",
    );
    expect(
      screen.getByLabelText("Chicken bowl quantity in servings"),
    ).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: "Close logging" }));
    openGlobalLog();
    choose("Meal");
    expectDate("2026-09-05");
    expect(
      screen.queryByLabelText("Chicken bowl quantity in servings"),
    ).toBeNull();
  });

  it("uses the latest Today route when an event omits its date", () => {
    const content = () => (
      <AppShell user={user}>
        <LogEntryButton kind="measurement">Check in</LogEntryButton>
      </AppShell>
    );
    const view = render(content());
    route.query = "date=2026-09-04";
    view.rerender(content());
    fireEvent.click(screen.getByRole("button", { name: "Check in" }));
    expectDate("2026-09-04");
  });

  it("validates the date used by the legacy meal logging link", () => {
    route.query = "log=meal&date=2026-09-07";
    render(<AppShell user={user}>Today</AppShell>);
    expectDate("2026-09-06");
  });
});
