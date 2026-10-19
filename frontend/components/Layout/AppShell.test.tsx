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
const stopImpersonation = vi.hoisted(() => vi.fn());
vi.mock("@/lib/auth-client", () => ({
  signOut: vi.fn(),
  stopImpersonation,
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

  describe("while an administrator views the site as the user", () => {
    const viewing: User = {
      ...user,
      impersonation: {
        impersonatorId: "admin-1",
        impersonatorName: "Ada Admin",
        expiresAt: "2026-09-07T01:45:00Z",
      },
    };

    it("says whose account it is and how long is left, and cannot be dismissed", () => {
      render(<AppShell user={viewing}>Today</AppShell>);

      const banner = screen.getByRole("status");
      expect(banner.textContent).toContain("Viewing as Test User");
      expect(banner.textContent).toContain("45 min left");
      expect(banner.textContent).toContain("recorded under your name");
      expect(screen.queryByRole("button", { name: /dismiss|close/i })).toBeNull();
    });

    it("is absent for an ordinary session", () => {
      render(<AppShell user={user}>Today</AppShell>);
      expect(screen.queryByText(/Viewing as/)).toBeNull();
    });

    it("leaves through the API and loads the admin area afresh", async () => {
      const assign = vi.fn();
      vi.stubGlobal("location", { ...window.location, assign });
      stopImpersonation.mockResolvedValue({ restored: true });
      render(<AppShell user={viewing}>Today</AppShell>);

      fireEvent.click(screen.getByRole("button", { name: "Back to admin" }));

      await vi.waitFor(() => expect(assign).toHaveBeenCalledWith("/admin/users"));
      expect(stopImpersonation).toHaveBeenCalledTimes(1);
      vi.unstubAllGlobals();
    });

    it("sends the administrator to sign in when their own session has ended", async () => {
      const assign = vi.fn();
      vi.stubGlobal("location", { ...window.location, assign });
      stopImpersonation.mockResolvedValue({ restored: false });
      render(<AppShell user={viewing}>Today</AppShell>);

      fireEvent.click(screen.getByRole("button", { name: "Back to admin" }));

      await vi.waitFor(() => expect(assign).toHaveBeenCalledWith("/login"));
      vi.unstubAllGlobals();
    });
  });
});
