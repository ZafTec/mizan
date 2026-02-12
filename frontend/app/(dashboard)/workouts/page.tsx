"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { clientApi } from "@/lib/api.client";

interface WorkoutExercise {
    exerciseId: string;
    exerciseName: string;
    sets: number;
    reps: number;
    weightKg?: number;
    durationSeconds?: number;
    notes?: string;
}

export default function WorkoutsPage() {
    const router = useRouter();
    const [name, setName] = useState("");
    const [date, setDate] = useState(new Date().toISOString().split("T")[0]);
    const [durationMinutes, setDurationMinutes] = useState("");
    const [caloriesBurned, setCaloriesBurned] = useState("");
    const [exercises, setExercises] = useState<WorkoutExercise[]>([]);
    const [isSubmitting, setIsSubmitting] = useState(false);

    const [searchTerm, setSearchTerm] = useState("");
    const [searchResults, setSearchResults] = useState<any[]>([]);
    const [isSearching, setIsSearching] = useState(false);

    const handleSearch = async () => {
        if (!searchTerm.trim()) return;

        setIsSearching(true);
        try {
            const result = await clientApi<{ items: any[] }>(
                `/api/Exercises?SearchTerm=${encodeURIComponent(searchTerm)}&PageSize=10`
            );
            setSearchResults(result.items || []);
        } catch (error) {
            console.error("Failed to search exercises:", error);
        } finally {
            setIsSearching(false);
        }
    };

    const handleAddExercise = (exercise: any) => {
        setExercises([
            ...exercises,
            {
                exerciseId: exercise.id,
                exerciseName: exercise.name,
                sets: 3,
                reps: 10,
            },
        ]);
        setSearchTerm("");
        setSearchResults([]);
    };

    const handleRemoveExercise = (index: number) => {
        setExercises(exercises.filter((_, i) => i !== index));
    };

    const handleUpdateExercise = (index: number, field: keyof WorkoutExercise, value: any) => {
        const updated = [...exercises];
        updated[index] = { ...updated[index], [field]: value };
        setExercises(updated);
    };

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();

        if (!name || !date || !durationMinutes || exercises.length === 0) {
            alert("Please fill in all required fields and add at least one exercise");
            return;
        }

        setIsSubmitting(true);
        try {
            await clientApi("/api/Workouts", {
                method: "POST",
                body: {
                    name,
                    date,
                    durationMinutes: Number(durationMinutes),
                    caloriesBurned: caloriesBurned ? Number(caloriesBurned) : null,
                    exercises: exercises.map(({ exerciseId, sets, reps, weightKg, durationSeconds, notes }) => ({
                        exerciseId,
                        sets,
                        reps,
                        weightKg: weightKg || null,
                        durationSeconds: durationSeconds || null,
                        notes: notes || null,
                    })),
                },
            });

            setName("");
            setDate(new Date().toISOString().split("T")[0]);
            setDurationMinutes("");
            setCaloriesBurned("");
            setExercises([]);
            alert("Workout logged successfully!");
            router.refresh();
        } catch (error) {
            console.error("Failed to log workout:", error);
            alert("Failed to log workout");
        } finally {
            setIsSubmitting(false);
        }
    };

    return (
        <div className="space-y-8">
            <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
                <div>
                    <h1 className="text-2xl font-bold text-slate-900">Log Workout</h1>
                    <p className="text-slate-500 mt-1">Track your training sessions</p>
                </div>
            </div>

            <form onSubmit={handleSubmit} className="space-y-6">
                <div className="card p-6 space-y-4">
                    <h2 className="section-title">Workout Details</h2>

                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                        <div>
                            <label className="label">Workout Name *</label>
                            <input
                                type="text"
                                value={name}
                                onChange={(e) => setName(e.target.value)}
                                placeholder="e.g., Morning Strength Training"
                                className="input"
                                required
                            />
                        </div>

                        <div>
                            <label className="label">Date *</label>
                            <input
                                type="date"
                                value={date}
                                onChange={(e) => setDate(e.target.value)}
                                className="input"
                                required
                            />
                        </div>

                        <div>
                            <label className="label">Duration (minutes) *</label>
                            <input
                                type="number"
                                value={durationMinutes}
                                onChange={(e) => setDurationMinutes(e.target.value)}
                                placeholder="60"
                                className="input"
                                required
                            />
                        </div>

                        <div>
                            <label className="label">Calories Burned</label>
                            <input
                                type="number"
                                value={caloriesBurned}
                                onChange={(e) => setCaloriesBurned(e.target.value)}
                                placeholder="300"
                                className="input"
                            />
                        </div>
                    </div>
                </div>

                <div className="card p-6 space-y-4">
                    <h2 className="section-title">Add Exercises</h2>

                    <div className="flex gap-2">
                        <input
                            type="text"
                            value={searchTerm}
                            onChange={(e) => setSearchTerm(e.target.value)}
                            onKeyDown={(e) => e.key === "Enter" && (e.preventDefault(), handleSearch())}
                            placeholder="Search exercises..."
                            className="input flex-1"
                        />
                        <button
                            type="button"
                            onClick={handleSearch}
                            disabled={isSearching}
                            className="btn-primary"
                        >
                            <i className="ri-search-line" />
                            Search
                        </button>
                    </div>

                    {searchResults.length > 0 && (
                        <div className="border border-slate-200 rounded-xl divide-y divide-slate-100">
                            {searchResults.map((exercise) => (
                                <button
                                    key={exercise.id}
                                    type="button"
                                    onClick={() => handleAddExercise(exercise)}
                                    className="w-full p-4 text-left hover:bg-slate-50 transition-colors flex items-center justify-between"
                                >
                                    <div>
                                        <div className="font-medium text-slate-900">{exercise.name}</div>
                                        <div className="text-sm text-slate-500">
                                            {exercise.category} • {exercise.muscleGroup || "N/A"}
                                        </div>
                                    </div>
                                    <i className="ri-add-line text-brand-500" />
                                </button>
                            ))}
                        </div>
                    )}

                    {exercises.length > 0 && (
                        <div className="space-y-3 mt-6">
                            <h3 className="font-semibold text-slate-900">Added Exercises</h3>
                            {exercises.map((exercise, index) => (
                                <div key={index} className="border border-slate-200 rounded-xl p-4 space-y-3">
                                    <div className="flex items-center justify-between">
                                        <h4 className="font-medium text-slate-900">{exercise.exerciseName}</h4>
                                        <button
                                            type="button"
                                            onClick={() => handleRemoveExercise(index)}
                                            className="text-red-500 hover:text-red-700"
                                        >
                                            <i className="ri-delete-bin-line" />
                                        </button>
                                    </div>

                                    <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
                                        <div>
                                            <label className="label text-xs">Sets</label>
                                            <input
                                                type="number"
                                                value={exercise.sets}
                                                onChange={(e) => handleUpdateExercise(index, "sets", Number(e.target.value))}
                                                className="input text-sm"
                                            />
                                        </div>
                                        <div>
                                            <label className="label text-xs">Reps</label>
                                            <input
                                                type="number"
                                                value={exercise.reps}
                                                onChange={(e) => handleUpdateExercise(index, "reps", Number(e.target.value))}
                                                className="input text-sm"
                                            />
                                        </div>
                                        <div>
                                            <label className="label text-xs">Weight (kg)</label>
                                            <input
                                                type="number"
                                                step="0.5"
                                                value={exercise.weightKg || ""}
                                                onChange={(e) => handleUpdateExercise(index, "weightKg", e.target.value ? Number(e.target.value) : undefined)}
                                                className="input text-sm"
                                            />
                                        </div>
                                        <div>
                                            <label className="label text-xs">Duration (sec)</label>
                                            <input
                                                type="number"
                                                value={exercise.durationSeconds || ""}
                                                onChange={(e) => handleUpdateExercise(index, "durationSeconds", e.target.value ? Number(e.target.value) : undefined)}
                                                className="input text-sm"
                                            />
                                        </div>
                                    </div>

                                    <div>
                                        <label className="label text-xs">Notes</label>
                                        <input
                                            type="text"
                                            value={exercise.notes || ""}
                                            onChange={(e) => handleUpdateExercise(index, "notes", e.target.value)}
                                            placeholder="Optional notes..."
                                            className="input text-sm"
                                        />
                                    </div>
                                </div>
                            ))}
                        </div>
                    )}
                </div>

                <button
                    type="submit"
                    disabled={isSubmitting || exercises.length === 0}
                    className="btn-primary w-full"
                >
                    {isSubmitting ? "Logging Workout..." : "Log Workout"}
                </button>
            </form>
        </div>
    );
}
