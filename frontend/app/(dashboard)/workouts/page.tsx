"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { clientApi } from "@/lib/api.client";
import { appToast } from "@/lib/toast";
import Loading from "@/components/Loading";

type ExerciseCategory = "Strength" | "Cardio" | "Flexibility" | "Balance" | string;

interface WorkoutExercise {
    exerciseId: string;
    exerciseName: string;
    category: ExerciseCategory;
    sets: number;
    reps: number;
    weightKg?: number;
    durationSeconds?: number;
    notes?: string;
}

function isStrength(category: ExerciseCategory) {
    return category === "Strength";
}

function isCardio(category: ExerciseCategory) {
    return category === "Cardio";
}

function getDefaults(category: ExerciseCategory): Pick<WorkoutExercise, "sets" | "reps" | "durationSeconds"> {
    if (isCardio(category)) return { sets: 1, reps: 1, durationSeconds: 1800 };
    if (category === "Flexibility") return { sets: 1, reps: 1, durationSeconds: 300 };
    return { sets: 3, reps: 10 };
}

type Tab = "log" | "library";

export default function WorkoutsPage() {
    const router = useRouter();
    const [activeTab, setActiveTab] = useState<Tab>("log");

    // Workout form state
    const [name, setName] = useState("");
    const [date, setDate] = useState(new Date().toISOString().split("T")[0]);
    const [durationMinutes, setDurationMinutes] = useState("");
    const [caloriesBurned, setCaloriesBurned] = useState("");
    const [exercises, setExercises] = useState<WorkoutExercise[]>([]);
    const [isSubmitting, setIsSubmitting] = useState(false);

    // Exercise search state (shared)
    const [searchTerm, setSearchTerm] = useState("");
    const [searchResults, setSearchResults] = useState<any[]>([]);
    const [isSearching, setIsSearching] = useState(false);

    // Library state
    const [libraryResults, setLibraryResults] = useState<any[]>([]);
    const [librarySearch, setLibrarySearch] = useState("");
    const [libraryCategory, setLibraryCategory] = useState("");
    const [libraryPage, setLibraryPage] = useState(1);
    const [libraryTotal, setLibraryTotal] = useState(0);
    const [libraryTotalPages, setLibraryTotalPages] = useState(0);
    const [isLoadingLibrary, setIsLoadingLibrary] = useState(false);
    const [libraryLoaded, setLibraryLoaded] = useState(false);

    const handleSearch = async () => {
        if (!searchTerm.trim()) return;
        setIsSearching(true);
        try {
            const result = await clientApi<{ items: any[] }>(
                `/api/Exercises?SearchTerm=${encodeURIComponent(searchTerm)}&PageSize=10`
            );
            setSearchResults(result.items || []);
        } catch {
            appToast.error("Failed to search exercises");
        } finally {
            setIsSearching(false);
        }
    };

    const loadLibrary = async (page = 1, search = librarySearch, category = libraryCategory) => {
        setIsLoadingLibrary(true);
        try {
            const params = new URLSearchParams({ Page: String(page), PageSize: "18" });
            if (search) params.set("SearchTerm", search);
            if (category) params.set("Category", category);

            const result = await clientApi<{ items: any[]; totalCount: number; totalPages: number }>(
                `/api/Exercises?${params}`
            );
            setLibraryResults(result.items || []);
            setLibraryTotal(result.totalCount || 0);
            setLibraryTotalPages(result.totalPages || 0);
            setLibraryPage(page);
            setLibraryLoaded(true);
        } catch {
            appToast.error("Failed to load exercises");
        } finally {
            setIsLoadingLibrary(false);
        }
    };

    const handleTabChange = (tab: Tab) => {
        setActiveTab(tab);
        if (tab === "library" && !libraryLoaded) {
            loadLibrary();
        }
    };

    const handleAddExercise = (exercise: any) => {
        const category = exercise.category || "Strength";
        const defaults = getDefaults(category);
        setExercises([
            ...exercises,
            {
                exerciseId: exercise.id,
                exerciseName: exercise.name,
                category,
                ...defaults,
            },
        ]);
        setSearchTerm("");
        setSearchResults([]);
        if (activeTab === "library") {
            setActiveTab("log");
            appToast.success(`Added ${exercise.name}`);
        }
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
            appToast.error("Please fill in all required fields and add at least one exercise");
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
            appToast.success("Workout logged");
            router.refresh();
        } catch (error) {
            appToast.error(error, "Failed to log workout");
        } finally {
            setIsSubmitting(false);
        }
    };

    const renderExerciseFields = (exercise: WorkoutExercise, index: number) => {
        const cat = exercise.category;

        if (isCardio(cat)) {
            return (
                <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
                    <div>
                        <label className="label text-xs">Duration (min)</label>
                        <input
                            type="number"
                            value={exercise.durationSeconds ? Math.round(exercise.durationSeconds / 60) : ""}
                            onChange={(e) => handleUpdateExercise(index, "durationSeconds", e.target.value ? Number(e.target.value) * 60 : undefined)}
                            placeholder="30"
                            className="input text-sm"
                        />
                    </div>
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
                        <label className="label text-xs">Weight (kg)</label>
                        <input
                            type="number"
                            step="0.5"
                            value={exercise.weightKg || ""}
                            onChange={(e) => handleUpdateExercise(index, "weightKg", e.target.value ? Number(e.target.value) : undefined)}
                            placeholder="Optional"
                            className="input text-sm"
                        />
                    </div>
                </div>
            );
        }

        if (cat === "Flexibility" || cat === "Balance") {
            return (
                <div className="grid grid-cols-2 gap-3">
                    <div>
                        <label className="label text-xs">Duration (min)</label>
                        <input
                            type="number"
                            value={exercise.durationSeconds ? Math.round(exercise.durationSeconds / 60) : ""}
                            onChange={(e) => handleUpdateExercise(index, "durationSeconds", e.target.value ? Number(e.target.value) * 60 : undefined)}
                            placeholder="10"
                            className="input text-sm"
                        />
                    </div>
                    <div>
                        <label className="label text-xs">Sets</label>
                        <input
                            type="number"
                            value={exercise.sets}
                            onChange={(e) => handleUpdateExercise(index, "sets", Number(e.target.value))}
                            className="input text-sm"
                        />
                    </div>
                </div>
            );
        }

        // Strength (default)
        return (
            <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
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
            </div>
        );
    };

    const categoryBadge = (cat: string) => {
        const colors: Record<string, string> = {
            Strength: "bg-brand-100 text-brand-700 dark:bg-brand-500/20 dark:text-brand-300",
            Cardio: "bg-red-100 text-red-700 dark:bg-red-500/20 dark:text-red-300",
            Flexibility: "bg-amber-100 text-amber-700 dark:bg-amber-500/20 dark:text-amber-300",
            Balance: "bg-indigo-100 text-indigo-700 dark:bg-indigo-500/20 dark:text-indigo-300",
        };
        return colors[cat] || "bg-slate-100 text-slate-600 dark:bg-slate-700 dark:text-slate-300";
    };

    return (
        <div className="space-y-6" data-testid="workouts-page">
            <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
                <div>
                    <h1 className="text-2xl font-bold text-slate-900 dark:text-slate-100">Workouts</h1>
                    <p className="text-slate-500 dark:text-slate-400 mt-1">Log workouts and browse exercises</p>
                </div>
            </div>

            {/* Tabs */}
            <div className="flex gap-1 p-1 bg-slate-100 dark:bg-slate-800 rounded-xl w-fit">
                <button
                    type="button"
                    onClick={() => handleTabChange("log")}
                    className={`px-4 py-2 rounded-lg text-sm font-medium transition-colors ${
                        activeTab === "log"
                            ? "bg-white dark:bg-slate-900 text-slate-900 dark:text-slate-100 shadow-sm"
                            : "text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-slate-200"
                    }`}
                >
                    <i className="ri-add-circle-line mr-1.5" />
                    Log Workout
                </button>
                <button
                    type="button"
                    onClick={() => handleTabChange("library")}
                    className={`px-4 py-2 rounded-lg text-sm font-medium transition-colors ${
                        activeTab === "library"
                            ? "bg-white dark:bg-slate-900 text-slate-900 dark:text-slate-100 shadow-sm"
                            : "text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-slate-200"
                    }`}
                >
                    <i className="ri-run-line mr-1.5" />
                    Exercise Library
                </button>
            </div>

            {/* Log Workout Tab */}
            {activeTab === "log" && (
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
                        <h2 className="section-title">Exercises</h2>

                        <div className="flex gap-2">
                            <input
                                type="text"
                                value={searchTerm}
                                onChange={(e) => setSearchTerm(e.target.value)}
                                onKeyDown={(e) => e.key === "Enter" && (e.preventDefault(), handleSearch())}
                                placeholder="Search exercises to add..."
                                className="input flex-1"
                                data-testid="search-input"
                            />
                            <button type="button" onClick={handleSearch} disabled={isSearching} className="btn-primary">
                                {isSearching ? <Loading size="sm" /> : <i className="ri-search-line" />}
                                Search
                            </button>
                        </div>

                        <p className="text-xs text-slate-500 dark:text-slate-400">
                            Or <button type="button" onClick={() => handleTabChange("library")} className="text-brand-500 hover:text-brand-600 font-medium">browse the exercise library</button> to find exercises
                        </p>

                        {searchResults.length > 0 && (
                            <div className="border border-slate-200 dark:border-slate-700 rounded-xl divide-y divide-slate-100 dark:divide-slate-800">
                                {searchResults.map((exercise) => (
                                    <button
                                        key={exercise.id}
                                        type="button"
                                        onClick={() => handleAddExercise(exercise)}
                                        className="w-full p-3 text-left hover:bg-slate-50 dark:hover:bg-slate-800 transition-colors flex items-center justify-between"
                                    >
                                        <div>
                                            <div className="font-medium text-slate-900 dark:text-slate-100">{exercise.name}</div>
                                            <div className="flex items-center gap-2 mt-1">
                                                <span className={`px-2 py-0.5 rounded-full text-xs font-medium ${categoryBadge(exercise.category)}`}>
                                                    {exercise.category}
                                                </span>
                                                {exercise.muscleGroup && (
                                                    <span className="text-xs text-slate-500 dark:text-slate-400">{exercise.muscleGroup}</span>
                                                )}
                                            </div>
                                        </div>
                                        <i className="ri-add-line text-brand-500" />
                                    </button>
                                ))}
                            </div>
                        )}

                        {exercises.length > 0 && (
                            <div className="space-y-3 mt-4">
                                <h3 className="font-semibold text-slate-900 dark:text-slate-100">
                                    Added ({exercises.length})
                                </h3>
                                {exercises.map((exercise, index) => (
                                    <div key={index} className="border border-slate-200 dark:border-slate-700 rounded-xl p-4 space-y-3">
                                        <div className="flex items-center justify-between">
                                            <div className="flex items-center gap-2">
                                                <h4 className="font-medium text-slate-900 dark:text-slate-100">{exercise.exerciseName}</h4>
                                                <span className={`px-2 py-0.5 rounded-full text-xs font-medium ${categoryBadge(exercise.category)}`}>
                                                    {exercise.category}
                                                </span>
                                            </div>
                                            <button
                                                type="button"
                                                onClick={() => handleRemoveExercise(index)}
                                                className="text-red-500 hover:text-red-700"
                                            >
                                                <i className="ri-delete-bin-line" />
                                            </button>
                                        </div>

                                        {renderExerciseFields(exercise, index)}

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
                        {isSubmitting ? (
                            <>
                                <Loading size="sm" />
                                Logging Workout...
                            </>
                        ) : (
                            "Log Workout"
                        )}
                    </button>
                </form>
            )}

            {/* Exercise Library Tab */}
            {activeTab === "library" && (
                <div className="space-y-6">
                    <div className="card p-6 space-y-4">
                        <div className="flex flex-col sm:flex-row gap-3">
                            <input
                                type="text"
                                value={librarySearch}
                                onChange={(e) => setLibrarySearch(e.target.value)}
                                onKeyDown={(e) => {
                                    if (e.key === "Enter") {
                                        e.preventDefault();
                                        loadLibrary(1, librarySearch, libraryCategory);
                                    }
                                }}
                                placeholder="Search exercises..."
                                className="input flex-1"
                            />
                            <select
                                value={libraryCategory}
                                onChange={(e) => {
                                    setLibraryCategory(e.target.value);
                                    loadLibrary(1, librarySearch, e.target.value);
                                }}
                                className="input sm:w-40"
                            >
                                <option value="">All Categories</option>
                                <option value="Strength">Strength</option>
                                <option value="Cardio">Cardio</option>
                                <option value="Flexibility">Flexibility</option>
                                <option value="Balance">Balance</option>
                            </select>
                            <button
                                type="button"
                                onClick={() => loadLibrary(1, librarySearch, libraryCategory)}
                                disabled={isLoadingLibrary}
                                className="btn-primary"
                            >
                                {isLoadingLibrary ? <Loading size="sm" /> : <i className="ri-search-line" />}
                                Search
                            </button>
                        </div>

                        <p className="text-sm text-slate-500 dark:text-slate-400">
                            {libraryTotal} exercises found
                        </p>
                    </div>

                    {libraryResults.length > 0 ? (
                        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                            {libraryResults.map((exercise) => {
                                const isAdded = exercises.some((e) => e.exerciseId === exercise.id);
                                return (
                                    <div key={exercise.id} className="card-hover p-5">
                                        <div className="flex items-start gap-4">
                                            <div className="w-10 h-10 rounded-xl bg-brand-600 dark:bg-brand-500 flex items-center justify-center shrink-0">
                                                <i className={`${isCardio(exercise.category) ? "ri-run-line" : isStrength(exercise.category) ? "ri-boxing-line" : "ri-mental-health-line"} text-lg text-white`} />
                                            </div>
                                            <div className="flex-1 min-w-0">
                                                <h3 className="font-semibold text-slate-900 dark:text-slate-100 truncate">{exercise.name}</h3>
                                                {exercise.description && (
                                                    <p className="text-sm text-slate-600 dark:text-slate-400 mt-1 line-clamp-2">{exercise.description}</p>
                                                )}
                                                <div className="flex flex-wrap gap-2 mt-2">
                                                    <span className={`px-2 py-0.5 rounded-full text-xs font-medium ${categoryBadge(exercise.category)}`}>
                                                        {exercise.category}
                                                    </span>
                                                    {exercise.muscleGroup && (
                                                        <span className="px-2 py-0.5 rounded-full bg-accent-100 text-accent-700 dark:bg-accent-500/20 dark:text-accent-300 text-xs font-medium">
                                                            {exercise.muscleGroup}
                                                        </span>
                                                    )}
                                                    {exercise.equipment && (
                                                        <span className="px-2 py-0.5 rounded-full bg-slate-100 text-slate-600 dark:bg-slate-700 dark:text-slate-300 text-xs font-medium">
                                                            {exercise.equipment}
                                                        </span>
                                                    )}
                                                </div>
                                            </div>
                                        </div>
                                        <div className="mt-3 flex items-center justify-between">
                                            {exercise.videoUrl && (
                                                <Link
                                                    href={exercise.videoUrl}
                                                    target="_blank"
                                                    rel="noopener noreferrer"
                                                    className="text-sm text-brand-600 hover:text-brand-700 dark:text-brand-400 flex items-center gap-1"
                                                >
                                                    <i className="ri-play-circle-line" />
                                                    Video
                                                </Link>
                                            )}
                                            <button
                                                type="button"
                                                onClick={() => handleAddExercise(exercise)}
                                                disabled={isAdded}
                                                className={`ml-auto text-sm font-medium px-3 py-1.5 rounded-lg transition-colors ${
                                                    isAdded
                                                        ? "bg-green-100 text-green-700 dark:bg-green-500/20 dark:text-green-300"
                                                        : "bg-brand-100 text-brand-700 hover:bg-brand-200 dark:bg-brand-500/20 dark:text-brand-300 dark:hover:bg-brand-500/30"
                                                }`}
                                            >
                                                {isAdded ? (
                                                    <><i className="ri-check-line mr-1" />Added</>
                                                ) : (
                                                    <><i className="ri-add-line mr-1" />Add to Workout</>
                                                )}
                                            </button>
                                        </div>
                                    </div>
                                );
                            })}
                        </div>
                    ) : libraryLoaded && !isLoadingLibrary ? (
                        <div className="card p-16 text-center">
                            <i className="ri-run-line text-4xl text-slate-400 dark:text-slate-500 mb-3 block" />
                            <h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100 mb-2">No exercises found</h3>
                            <p className="text-slate-500 dark:text-slate-400">Try a different search or category</p>
                        </div>
                    ) : null}

                    {/* Library Pagination */}
                    {libraryTotalPages > 1 && (
                        <div className="flex justify-center items-center gap-2">
                            <button
                                type="button"
                                onClick={() => loadLibrary(libraryPage - 1)}
                                disabled={libraryPage <= 1 || isLoadingLibrary}
                                className="w-10 h-10 flex items-center justify-center rounded-xl bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-400 transition-colors disabled:opacity-50 hover:bg-slate-200 dark:hover:bg-slate-700"
                            >
                                <i className="ri-arrow-left-s-line" />
                            </button>
                            <span className="text-sm text-slate-500 dark:text-slate-400">
                                Page {libraryPage} of {libraryTotalPages}
                            </span>
                            <button
                                type="button"
                                onClick={() => loadLibrary(libraryPage + 1)}
                                disabled={libraryPage >= libraryTotalPages || isLoadingLibrary}
                                className="w-10 h-10 flex items-center justify-center rounded-xl bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-400 transition-colors disabled:opacity-50 hover:bg-slate-200 dark:hover:bg-slate-700"
                            >
                                <i className="ri-arrow-right-s-line" />
                            </button>
                        </div>
                    )}

                    {exercises.length > 0 && (
                        <div className="sticky bottom-4 z-10">
                            <button
                                type="button"
                                onClick={() => setActiveTab("log")}
                                className="btn-primary w-full shadow-lg"
                            >
                                <i className="ri-arrow-left-line mr-1.5" />
                                Back to Workout ({exercises.length} exercise{exercises.length !== 1 ? "s" : ""} added)
                            </button>
                        </div>
                    )}
                </div>
            )}
        </div>
    );
}
