import { describe, expect, it } from "vitest";
import { createEmptyDraft, workoutDraftReducer, type WorkoutDraft } from "./draft";

const base: WorkoutDraft = {
  ...createEmptyDraft(new Date("2026-07-18T12:00:00Z")),
  exercises: [{
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
  }],
};

describe("workout draft reducer", () => {
  it("cycles target reps down to an incomplete set", () => {
    let state = workoutDraftReducer(base, { type: "cycle-reps", exerciseUid: "exercise", setUid: "a", now: "2026-07-18T12:01:00Z" });
    expect(state.exercises[0].sets[0].repsCompleted).toBe(5);
    expect(state.restTimer?.seconds).toBe(90);
    state = workoutDraftReducer(state, { type: "cycle-reps", exerciseUid: "exercise", setUid: "a", now: "2026-07-18T12:02:00Z" });
    expect(state.exercises[0].sets[0].repsCompleted).toBe(4);
    for (let count = 0; count < 5; count++) state = workoutDraftReducer(state, { type: "cycle-reps", exerciseUid: "exercise", setUid: "a", now: "2026-07-18T12:03:00Z" });
    expect(state.exercises[0].sets[0].repsCompleted).toBeUndefined();
  });

  it("applies weight only to uncompleted sets", () => {
    const completed = workoutDraftReducer(base, { type: "cycle-reps", exerciseUid: "exercise", setUid: "a", now: "2026-07-18T12:01:00Z" });
    const state = workoutDraftReducer(completed, { type: "apply-weight", exerciseUid: "exercise", setUid: "b", weightKg: 85, scope: "uncompleted" });
    expect(state.exercises[0].sets.map((set) => set.weightKg)).toEqual([80, 85]);
  });
});
