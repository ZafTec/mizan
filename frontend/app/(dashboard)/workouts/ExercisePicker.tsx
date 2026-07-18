"use client";

import type { Dispatch } from "react";
import { useState } from "react";
import { clientApi } from "@/lib/api.client";
import { appToast } from "@/lib/toast";
import type { WorkoutDraftAction } from "@/lib/workouts/draft";
import type { ExerciseDto } from "@/types/workout";

export default function ExercisePicker({
  dispatch,
}: {
  dispatch: Dispatch<WorkoutDraftAction>;
}) {
  const [search, setSearch] = useState("");
  const [results, setResults] = useState<ExerciseDto[]>([]);
  const [showCreate, setShowCreate] = useState(false);

  async function find() {
    if (!search.trim()) return;
    try {
      const result = await clientApi<{ items: ExerciseDto[] }>(
        `/api/Exercises?searchTerm=${encodeURIComponent(search)}&pageSize=12`,
      );
      setResults(result.items);
    } catch (error) {
      appToast.error(error, "Could not search exercises");
    }
  }

  function add(exercise: ExerciseDto) {
    const cardio = exercise.category === "Cardio";
    dispatch({
      type: "add-exercise",
      exercise: {
        uid: crypto.randomUUID(),
        exerciseId: exercise.id,
        name: exercise.name,
        category: exercise.category,
        supersetWithNext: false,
        restSecondsMin: 60,
        restSecondsMax: 120,
        sets: Array.from({ length: cardio ? 1 : 3 }, () => ({
          uid: crypto.randomUUID(),
          targetReps: cardio ? undefined : 10,
          weightKg: 0,
          durationSeconds: cardio ? 1800 : undefined,
        })),
      },
    });
    setResults([]);
    setSearch("");
  }

  return (
    <div className="card p-4">
      <div className="flex gap-2">
        <input
          className="input"
          value={search}
          onChange={(event) => setSearch(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === "Enter") {
              event.preventDefault();
              find();
            }
          }}
          placeholder="Search exercises"
        />
        <button className="btn-primary" onClick={find}>
          Search
        </button>
      </div>
      {results.length > 0 && (
        <div className="mt-3 grid gap-2 sm:grid-cols-2">
          {results.map((exercise) => (
            <button
              key={exercise.id}
              className="press-feedback flex items-center justify-between rounded-2xl border border-charcoal-blue-200 p-3 text-left dark:border-white/10"
              onClick={() => add(exercise)}
            >
              <span>
                <strong className="block text-sm">{exercise.name}</strong>
                <span className="text-xs text-charcoal-blue-500">
                  {exercise.category}
                  {exercise.muscleGroup ? ` · ${exercise.muscleGroup}` : ""}
                </span>
              </span>
              <span className="text-brand-600">Add</span>
            </button>
          ))}
        </div>
      )}
      {search && results.length === 0 && (
        <button
          className="mt-3 text-sm font-semibold text-brand-700"
          onClick={() => setShowCreate(true)}
        >
          Can&apos;t find it? Create exercise
        </button>
      )}
      {showCreate && (
        <CreateExercise
          initialName={search}
          onClose={() => setShowCreate(false)}
          onCreated={add}
        />
      )}
    </div>
  );
}

function CreateExercise({
  initialName,
  onClose,
  onCreated,
}: {
  initialName: string;
  onClose: () => void;
  onCreated: (exercise: ExerciseDto) => void;
}) {
  const [name, setName] = useState(initialName);
  const [category, setCategory] = useState("Strength");
  const [muscleGroup, setMuscleGroup] = useState("");
  const [equipment, setEquipment] = useState("");

  async function create() {
    try {
      const result = await clientApi<{ id: string; name: string }>(
        "/api/Exercises",
        {
          method: "POST",
          body: {
            name,
            category,
            muscleGroup: muscleGroup || null,
            equipment: equipment || null,
          },
        },
      );
      onCreated({
        id: result.id,
        name: result.name,
        category,
        muscleGroup,
        equipment,
        isCustom: true,
        isApproved: false,
        isOwner: true,
      });
      onClose();
    } catch (error) {
      appToast.error(error, "Could not create exercise");
    }
  }

  return (
    <div className="mt-4 grid gap-3 rounded-2xl border border-brand-300 bg-brand-50 p-4 dark:border-brand-500/30 dark:bg-brand-950/30">
      <h3 className="font-semibold">Create custom exercise</h3>
      <input
        className="input"
        value={name}
        onChange={(event) => setName(event.target.value)}
        placeholder="Exercise name"
      />
      <select
        className="input"
        value={category}
        onChange={(event) => setCategory(event.target.value)}
      >
        <option>Strength</option>
        <option>Cardio</option>
        <option>Flexibility</option>
        <option>Balance</option>
      </select>
      <div className="grid gap-3 sm:grid-cols-2">
        <input
          className="input"
          value={muscleGroup}
          onChange={(event) => setMuscleGroup(event.target.value)}
          placeholder="Muscle group"
        />
        <input
          className="input"
          value={equipment}
          onChange={(event) => setEquipment(event.target.value)}
          placeholder="Equipment"
        />
      </div>
      <div className="flex gap-2">
        <button
          className="btn-primary btn-sm"
          onClick={create}
          disabled={!name.trim()}
        >
          Create and add
        </button>
        <button className="btn-ghost btn-sm" onClick={onClose}>
          Cancel
        </button>
      </div>
    </div>
  );
}
