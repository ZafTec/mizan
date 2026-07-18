import { describe, expect, it } from "vitest";
import {
  createEmptyDraft,
  newerStoredDraft,
  parseStoredDraft,
  serializeStoredDraft,
  workoutDraftReducer,
  type WorkoutDraft,
} from "./draft";

const base: WorkoutDraft = {
  ...createEmptyDraft(new Date("2026-07-18T12:00:00Z")),
  exercises: [
    {
      uid: "exercise",
      exerciseId: "id",
      name: "Bench Press",
      category: "Strength",
      supersetWithNext: false,
      restSecondsMax: 90,
      sets: [
        { uid: "a", targetReps: 5, weightKg: 80 },
        { uid: "b", targetReps: 5, weightKg: 80 },
      ],
    },
  ],
};

describe("workout draft reducer", () => {
  it("cycles target reps down to an incomplete set", () => {
    let state = workoutDraftReducer(base, {
      type: "cycle-reps",
      exerciseUid: "exercise",
      setUid: "a",
      now: "2026-07-18T12:01:00Z",
    });
    expect(state.exercises[0].sets[0].repsCompleted).toBe(5);
    expect(state.restTimer?.seconds).toBe(90);
    state = workoutDraftReducer(state, {
      type: "cycle-reps",
      exerciseUid: "exercise",
      setUid: "a",
      now: "2026-07-18T12:02:00Z",
    });
    expect(state.exercises[0].sets[0].repsCompleted).toBe(4);
    expect(state.restTimer?.startedAt).toBe("2026-07-18T12:01:00Z");
    for (let count = 0; count < 5; count++)
      state = workoutDraftReducer(state, {
        type: "cycle-reps",
        exerciseUid: "exercise",
        setUid: "a",
        now: "2026-07-18T12:03:00Z",
      });
    expect(state.exercises[0].sets[0].repsCompleted).toBeUndefined();
  });

  it("keeps the newest local or server draft", () => {
    const local = parseStoredDraft(
      serializeStoredDraft(base, "2026-07-18T12:05:00Z"),
    );
    const server = parseStoredDraft(
      JSON.stringify({ ...base, name: "Server" }),
      "2026-07-18T12:04:00Z",
    );
    expect(newerStoredDraft(local, server)?.draft.name).toBe(base.name);
    expect(newerStoredDraft(null, server)?.draft.name).toBe("Server");
  });

  it("applies weight only to uncompleted sets", () => {
    const completed = workoutDraftReducer(base, {
      type: "cycle-reps",
      exerciseUid: "exercise",
      setUid: "a",
      now: "2026-07-18T12:01:00Z",
    });
    const state = workoutDraftReducer(completed, {
      type: "apply-weight",
      exerciseUid: "exercise",
      setUid: "b",
      weightKg: 85,
      scope: "uncompleted",
    });
    expect(state.exercises[0].sets.map((set) => set.weightKg)).toEqual([
      80, 85,
    ]);
  });
});
