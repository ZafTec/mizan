import {
  act,
  cleanup,
  fireEvent,
  render,
  screen,
} from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { clientApi } from "@/lib/api.client";
import type { WorkoutDraft } from "@/lib/workouts/draft";
import type { WorkoutSummaryDto } from "@/types/workout";
import WorkoutDashboard from "./WorkoutDashboard";

const { refresh, replace } = vi.hoisted(() => ({
  refresh: vi.fn(),
  replace: vi.fn(),
}));
vi.mock("next/navigation", () => ({
  useRouter: () => ({ refresh, replace }),
}));
vi.mock("@/lib/api.client", () => ({ clientApi: vi.fn() }));
vi.mock("@/lib/auth-client", () => ({
  useSession: () => ({ data: { user: { id: "current-user" } } }),
}));
vi.mock("@/lib/toast", () => ({
  appToast: { success: vi.fn(), error: vi.fn() },
}));

function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason: unknown) => void;
  const promise = new Promise<T>((yes, no) => {
    resolve = yes;
    reject = no;
  });
  return { promise, resolve, reject };
}

const selectedDate = "2026-09-01";
const draftKey = "mizan-workout-draft:current-user";
const lastWorkout: WorkoutSummaryDto = {
  id: "previous-workout",
  name: "Strength day",
  workoutDate: "2026-08-31",
  createdAt: "2026-08-31T12:00:00Z",
  exercises: [
    {
      id: "previous-exercise",
      exerciseId: "bench-press",
      exerciseName: "Bench press",
      category: "Strength",
      sortOrder: 0,
      supersetWithNext: false,
      sets: [{ setNumber: 1, reps: 5, weightKg: 80, completed: true }],
    },
  ],
};
const finishedWorkout = {
  id: "finished-workout",
  totalExercises: 1,
  totalSets: 1,
  personalRecords: [],
};

function mockPersistence() {
  const saves: Array<{
    draft: WorkoutDraft;
    response: ReturnType<typeof deferred<void>>;
  }> = [];
  const finish = deferred<typeof finishedWorkout>();
  let serverDraft: WorkoutDraft | null = null;
  const operations: string[] = [];

  vi.mocked(clientApi).mockImplementation(async (path, options) => {
    if (!options?.method) throw new Error("No existing draft");
    operations.push(options.method);
    if (path === "/api/Workouts/draft" && options.method === "PUT") {
      const draft = JSON.parse(
        (options.body as { payload: string }).payload,
      ) as WorkoutDraft;
      const response = deferred<void>();
      saves.push({ draft, response });
      await response.promise;
      serverDraft = draft;
      return undefined as never;
    }
    if (path === "/api/Workouts" && options.method === "POST") {
      return (await finish.promise) as never;
    }
    if (path === "/api/Workouts/draft" && options.method === "DELETE") {
      serverDraft = null;
      return undefined as never;
    }
    throw new Error(`Unexpected request: ${options.method} ${path}`);
  });
  return { saves, finish, operations, serverDraft: () => serverDraft };
}

async function startWorkout() {
  await act(async () => {
    render(
      <WorkoutDashboard
        initialTab="log"
        initialHistory={{ items: [lastWorkout], totalCount: 1 }}
        initialTemplates={[]}
        defaultPublishWorkouts={false}
        initialDate={selectedDate}
        sessionOnly
      />,
    );
  });
  fireEvent.click(screen.getByRole("button", { name: /Repeat last workout/ }));
}

function setWeight(weight: number) {
  fireEvent.change(screen.getByLabelText("Weight (kg) for set 1"), {
    target: { value: String(weight) },
  });
}

async function elapse(milliseconds = 600) {
  await act(async () => {
    await vi.advanceTimersByTimeAsync(milliseconds);
  });
}

beforeEach(() => {
  vi.useFakeTimers();
  vi.setSystemTime(new Date("2026-09-07T12:00:00Z"));
  vi.clearAllMocks();
  // Keep browser persistence independent of Node's optional localStorage file.
  const stored = new Map<string, string>();
  vi.stubGlobal("localStorage", {
    get length() {
      return stored.size;
    },
    clear: () => stored.clear(),
    getItem: (key: string) => stored.get(key) ?? null,
    key: (index: number) => [...stored.keys()][index] ?? null,
    removeItem: (key: string) => {
      stored.delete(key);
    },
    setItem: (key: string, value: string) => {
      stored.set(key, value);
    },
  } satisfies Storage);
});
afterEach(() => {
  cleanup();
  vi.useRealTimers();
  localStorage.clear();
  vi.unstubAllGlobals();
});

describe("workout draft persistence", () => {
  it("serializes slow autosaves and keeps the newest edited sets", async () => {
    const api = mockPersistence();
    await startWorkout();
    await elapse();
    expect(api.saves).toHaveLength(1);

    setWeight(85);
    await elapse();
    setWeight(90);
    await elapse();

    // Neither newer draft may reach the server while the old PUT is pending.
    expect(api.saves).toHaveLength(1);
    await act(async () => api.saves[0].response.resolve());
    expect(api.saves).toHaveLength(2);
    expect(api.saves[1].draft.exercises[0].sets[0].weightKg).toBe(90);
    await act(async () => api.saves[1].response.resolve());

    expect(api.serverDraft()?.workoutDate).toBe(selectedDate);
    expect(api.serverDraft()?.exercises[0].sets[0].weightKg).toBe(90);
    expect(screen.getByRole("status").textContent).toBe("Session saved");
    expect(JSON.parse(localStorage.getItem(draftKey)!).draft.workoutDate).toBe(
      selectedDate,
    );
    expect(localStorage.getItem("mizan-workout-draft")).toBeNull();
  });

  it("waits for an older PUT before finishing and never recreates the deleted draft", async () => {
    const api = mockPersistence();
    await startWorkout();
    await elapse();
    setWeight(90);
    fireEvent.click(screen.getByRole("button", { name: "Cycle set 1" }));
    await elapse();

    fireEvent.click(screen.getByRole("button", { name: "Finish workout" }));
    expect(screen.getByLabelText("Weight (kg) for set 1").matches(":disabled")).toBe(
      true,
    );
    expect(screen.getByLabelText("Workout name").matches(":disabled")).toBe(
      true,
    );
    await elapse(2_000);
    expect(api.operations).toEqual(["PUT"]);

    await act(async () => api.saves[0].response.resolve());
    expect(api.operations).toEqual(["PUT", "POST"]);
    expect(screen.getByLabelText("Weight (kg) for set 1").matches(":disabled")).toBe(
      true,
    );
    const finishCall = vi
      .mocked(clientApi)
      .mock.calls.find(
        ([path, options]) =>
          path === "/api/Workouts" && options?.method === "POST",
      );
    expect(finishCall?.[1]?.body).toMatchObject({
      workoutDate: selectedDate,
      exercises: [{ sets: [{ weightKg: 90, reps: 5, completed: true }] }],
    });

    await act(async () => api.finish.resolve(finishedWorkout));
    await elapse(5_000);
    expect(api.operations).toEqual(["PUT", "POST", "DELETE"]);
    expect(api.serverDraft()).toBeNull();
    expect(localStorage.getItem(draftKey)).toBeNull();
    expect(screen.getByRole("heading", { name: "Strong work" })).toBeTruthy();
    expect(refresh).toHaveBeenCalledOnce();
  });

  it("keeps the completed sets available when Finish fails", async () => {
    const api = mockPersistence();
    await startWorkout();
    setWeight(95);
    fireEvent.click(screen.getByRole("button", { name: "Cycle set 1" }));
    await elapse();
    fireEvent.click(screen.getByRole("button", { name: "Finish workout" }));

    await act(async () => api.saves[0].response.resolve());
    await act(async () => api.finish.reject(new Error("Connection lost")));

    expect(api.operations).toEqual(["PUT", "POST"]);
    const weight = screen.getByLabelText(
      "Weight (kg) for set 1",
    ) as HTMLInputElement;
    expect(weight.matches(":disabled")).toBe(false);
    expect(weight.value).toBe("95");
    expect(screen.getByRole("button", { name: "Finish workout" })).toBeTruthy();
    expect(
      JSON.parse(localStorage.getItem(draftKey)!).draft.exercises[0].sets[0],
    ).toMatchObject({
      weightKg: 95,
      repsCompleted: 5,
      completedAt: expect.any(String),
    });
    expect(api.serverDraft()?.exercises[0].sets[0].weightKg).toBe(95);
    expect(refresh).not.toHaveBeenCalled();
  });
});
