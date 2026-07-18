export type DraftSet = {
  uid: string;
  targetReps?: number;
  repsCompleted?: number;
  weightKg: number;
  durationSeconds?: number;
  distanceMeters?: number;
  completedAt?: string;
};

export type DraftExercise = {
  uid: string;
  exerciseId: string;
  name: string;
  category: string;
  notes?: string;
  supersetWithNext: boolean;
  restSecondsMin?: number;
  restSecondsMax?: number;
  sets: DraftSet[];
};

export type WorkoutDraft = {
  name: string;
  workoutDate: string;
  templateId?: string;
  bodyweightKg?: number;
  startedAt: string;
  exercises: DraftExercise[];
  restTimer?: { startedAt: string; seconds: number };
};

export type WeightScope = "set" | "uncompleted" | "all";

export type WorkoutDraftAction =
  | { type: "replace"; draft: WorkoutDraft }
  | { type: "set-field"; field: "name" | "workoutDate" | "bodyweightKg"; value: string | number | undefined }
  | { type: "add-exercise"; exercise: DraftExercise }
  | { type: "remove-exercise"; exerciseUid: string }
  | { type: "set-exercise-notes"; exerciseUid: string; notes: string }
  | { type: "toggle-superset"; exerciseUid: string }
  | { type: "add-set"; exerciseUid: string }
  | { type: "remove-set"; exerciseUid: string; setUid: string }
  | { type: "cycle-reps"; exerciseUid: string; setUid: string; now: string }
  | { type: "edit-set"; exerciseUid: string; setUid: string; patch: Partial<DraftSet> }
  | { type: "apply-weight"; exerciseUid: string; setUid: string; weightKg: number; scope: WeightScope }
  | { type: "clear-rest-timer" };

export function createEmptyDraft(now = new Date()): WorkoutDraft {
  return {
    name: "",
    workoutDate: now.toLocaleDateString("en-CA"),
    startedAt: now.toISOString(),
    exercises: [],
  };
}

export function workoutDraftReducer(state: WorkoutDraft, action: WorkoutDraftAction): WorkoutDraft {
  if (action.type === "replace") return action.draft;
  if (action.type === "set-field") return { ...state, [action.field]: action.value };
  if (action.type === "add-exercise") return { ...state, exercises: [...state.exercises, action.exercise] };
  if (action.type === "remove-exercise") return { ...state, exercises: state.exercises.filter((exercise) => exercise.uid !== action.exerciseUid) };
  if (action.type === "clear-rest-timer") return { ...state, restTimer: undefined };

  return {
    ...state,
    exercises: state.exercises.map((exercise) => {
      if (exercise.uid !== action.exerciseUid) return exercise;
      if (action.type === "set-exercise-notes") return { ...exercise, notes: action.notes };
      if (action.type === "toggle-superset") return { ...exercise, supersetWithNext: !exercise.supersetWithNext };
      if (action.type === "add-set") {
        const previous = exercise.sets.at(-1);
        return { ...exercise, sets: [...exercise.sets, { uid: crypto.randomUUID(), targetReps: previous?.targetReps, weightKg: previous?.weightKg ?? 0 }] };
      }
      if (action.type === "remove-set") return { ...exercise, sets: exercise.sets.filter((set) => set.uid !== action.setUid) };
      if (action.type === "apply-weight") {
        return {
          ...exercise,
          sets: exercise.sets.map((set) => {
            const applies = action.scope === "all" || set.uid === action.setUid || (action.scope === "uncompleted" && !set.completedAt);
            return applies ? { ...set, weightKg: action.weightKg } : set;
          }),
        };
      }
      return {
        ...exercise,
        sets: exercise.sets.map((set) => {
          if (set.uid !== action.setUid) return set;
          if (action.type === "edit-set") return { ...set, ...action.patch };
          if (action.type === "cycle-reps") {
            if (set.repsCompleted === undefined) return { ...set, repsCompleted: set.targetReps ?? 0, completedAt: action.now };
            if (set.repsCompleted > 0) return { ...set, repsCompleted: set.repsCompleted - 1, completedAt: action.now };
            return { ...set, repsCompleted: undefined, completedAt: undefined };
          }
          return set;
        }),
      };
    }),
    restTimer: restTimerForAction(state, action),
  };
}

function restTimerForAction(state: WorkoutDraft, action: WorkoutDraftAction) {
  if (action.type !== "cycle-reps") return state.restTimer;
  const exercise = state.exercises.find((item) => item.uid === action.exerciseUid);
  const set = exercise?.sets.find((item) => item.uid === action.setUid);
  if (set?.repsCompleted === 0) return undefined;
  return exercise?.restSecondsMax ? { startedAt: action.now, seconds: exercise.restSecondsMax } : state.restTimer;
}

export function draftFromTemplate(template: {
  templateId: string;
  name: string;
  exercises: Array<{
    exerciseId: string;
    name: string;
    category: string;
    notes?: string | null;
    supersetWithNext: boolean;
    restSecondsMin?: number | null;
    restSecondsMax?: number | null;
    sets: Array<{ targetReps?: number | null; weightKg: number; targetSeconds?: number | null; targetDistanceMeters?: number | null }>;
  }>;
}): WorkoutDraft {
  const draft = createEmptyDraft();
  return {
    ...draft,
    name: template.name,
    templateId: template.templateId,
    exercises: template.exercises.map((exercise) => ({
      uid: crypto.randomUUID(),
      exerciseId: exercise.exerciseId,
      name: exercise.name,
      category: exercise.category,
      notes: exercise.notes ?? undefined,
      supersetWithNext: exercise.supersetWithNext,
      restSecondsMin: exercise.restSecondsMin ?? undefined,
      restSecondsMax: exercise.restSecondsMax ?? undefined,
      sets: exercise.sets.map((set) => ({
        uid: crypto.randomUUID(),
        targetReps: set.targetReps ?? undefined,
        weightKg: set.weightKg,
        durationSeconds: set.targetSeconds ?? undefined,
        distanceMeters: set.targetDistanceMeters ?? undefined,
      })),
    })),
  };
}

export function toLogWorkoutPayload(draft: WorkoutDraft) {
  return {
    name: draft.name || "Workout",
    workoutDate: draft.workoutDate,
    templateId: draft.templateId ?? null,
    bodyweightKg: draft.bodyweightKg ?? null,
    startedAt: draft.startedAt,
    completedAt: new Date().toISOString(),
    exercises: draft.exercises.map((exercise) => ({
      exerciseId: exercise.exerciseId,
      notes: exercise.notes ?? null,
      supersetWithNext: exercise.supersetWithNext,
      sets: exercise.sets.map((set) => ({
        reps: set.repsCompleted ?? set.targetReps ?? null,
        weightKg: set.weightKg,
        durationSeconds: set.durationSeconds ?? null,
        distanceMeters: set.distanceMeters ?? null,
        completedAt: set.completedAt ?? null,
        completed: Boolean(set.completedAt),
      })),
    })),
  };
}
