import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { createEmptyDraft, type WorkoutDraft } from "@/lib/workouts/draft";
import { clientApi } from "@/lib/api.client";
import ActiveWorkout from "./ActiveWorkout";
import { PostWorkout } from "./WorkoutPrompts";

vi.mock("@/lib/api.client", () => ({ clientApi: vi.fn() }));
vi.mock("@/lib/toast", () => ({
  appToast: { success: vi.fn(), error: vi.fn() },
}));

const draft: WorkoutDraft = {
  ...createEmptyDraft(new Date("2026-07-18T12:00:00Z")),
  name: "Strength day",
  exercises: [
    {
      uid: "exercise",
      exerciseId: "exercise-id",
      name: "Bench Press",
      category: "Strength",
      supersetWithNext: false,
      sets: [
        { uid: "set-a", targetReps: 5, weightKg: 80 },
        { uid: "set-b", targetReps: 5, weightKg: 80 },
      ],
    },
  ],
};

describe("reviewed workout UI", () => {
  it("dispatches the selected weight scope", () => {
    const dispatch = vi.fn();
    render(
      <ActiveWorkout
        draft={draft}
        dispatch={dispatch}
        restRemaining={null}
        onFinish={vi.fn()}
      />,
    );

    fireEvent.change(screen.getByLabelText("Apply weight to"), {
      target: { value: "all" },
    });
    fireEvent.change(screen.getByLabelText("Weight for set 1"), {
      target: { value: "90" },
    });

    expect(dispatch).toHaveBeenCalledWith(
      expect.objectContaining({
        type: "apply-weight",
        weightKg: 90,
        scope: "all",
      }),
    );
  });

  it("uses the profile default and submits a caption", async () => {
    vi.mocked(clientApi).mockResolvedValueOnce({});
    const onClose = vi.fn();
    render(
      <PostWorkout
        defaultPublish
        summary={{
          id: "workout-id",
          exercises: 1,
          sets: 2,
          personalRecords: [],
        }}
        onClose={onClose}
      />,
    );

    expect((screen.getByRole("checkbox") as HTMLInputElement).checked).toBe(
      true,
    );
    fireEvent.change(screen.getByLabelText("Caption"), {
      target: { value: "New top set" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Share and continue" }));

    await waitFor(() =>
      expect(clientApi).toHaveBeenCalledWith(
        "/api/Social/feed",
        expect.objectContaining({
          method: "POST",
          body: expect.objectContaining({
            workoutId: "workout-id",
            caption: "New top set",
          }),
        }),
      ),
    );
    expect(onClose).toHaveBeenCalledOnce();
  });
});
