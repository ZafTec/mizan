import { getExercises } from "@/data/exercise";
import { getUserServer } from "@/helper/session";
import Pagination from "@/components/Pagination";
import SearchExercises from "./SearchExercises";
import Image from "next/image";
import Link from "next/link";

export const dynamic = 'force-dynamic';

const PAGE_SIZE = 18;

export default async function ExercisesPage({
    searchParams,
}: {
    searchParams: Promise<{ search?: string; category?: string; page?: string }>;
}) {
    await getUserServer();
    const params = await searchParams;
    const page = Math.max(1, parseInt(params.page || "1", 10) || 1);
    const { exercises, totalCount, totalPages } = await getExercises(params.search, params.category, page, PAGE_SIZE);

    const baseUrl = `/exercises?${[
        params.search ? `search=${encodeURIComponent(params.search)}` : "",
        params.category ? `category=${encodeURIComponent(params.category)}` : "",
    ].filter(Boolean).join("&")}`;

    return (
        <div className="space-y-6 lg:space-y-8" data-testid="exercises-page">
            <header className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
                <div className="space-y-2">
                    <p className="eyebrow">Library</p>
                    <h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
                        Exercises
                    </h1>
                    <p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
                        Browse {totalCount} exercises for your workouts.
                    </p>
                </div>
                <Link href="/workouts" className="btn-primary">
                    <i className="ri-add-circle-line" />
                    Log Workout
                </Link>
            </header>

            <SearchExercises initialSearch={params.search} initialCategory={params.category} />

            {exercises.length > 0 ? (
                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                    {exercises.map((exercise) => (
                        <div key={exercise.id} className="card-hover p-5">
                            <div className="flex items-start gap-4">
							<div className="w-12 h-12 rounded-2xl bg-brand-600 flex items-center justify-center shadow-lg shrink-0 relative overflow-hidden dark:bg-brand-500">
                                    {exercise.imageUrl ? (
                                        <Image
                                            src={exercise.imageUrl}
                                            alt={exercise.name}
                                            fill
                                            sizes="48px"
                                            className="object-cover rounded-2xl"
                                        />
                                    ) : (
                                        <i className="ri-run-line text-xl text-white" />
                                    )}
                                </div>
                                <div className="flex-1 min-w-0">
                                    <h3 className="font-semibold text-slate-900 dark:text-slate-100 truncate">
                                        {exercise.name}
                                    </h3>
                                    {exercise.description && (
                                        <p className="text-sm text-slate-600 mt-1 line-clamp-2">
                                            {exercise.description}
                                        </p>
                                    )}
                                    <div className="flex flex-wrap gap-2 mt-3">
                                        <span className="inline-flex items-center gap-1 px-2 py-1 rounded-full bg-brand-100 text-brand-700 text-xs font-medium">
                                            {exercise.category}
                                        </span>
                                        {exercise.muscleGroup && (
                                            <span className="inline-flex items-center gap-1 px-2 py-1 rounded-full bg-accent-100 text-accent-700 text-xs font-medium">
                                                {exercise.muscleGroup}
                                            </span>
                                        )}
                                        {exercise.equipment && (
                                            <span className="inline-flex items-center gap-1 px-2 py-1 rounded-full bg-slate-100 text-slate-600 text-xs font-medium">
                                                {exercise.equipment}
                                            </span>
                                        )}
                                    </div>
                                </div>
                            </div>
                            {exercise.videoUrl && (
                                <a
                                    href={exercise.videoUrl}
                                    target="_blank"
                                    rel="noopener noreferrer"
                                    className="mt-3 flex items-center gap-2 text-sm text-brand-600 hover:text-brand-700"
                                >
                                    <i className="ri-play-circle-line" />
                                    Watch video
                                </a>
                            )}
                        </div>
                    ))}
                </div>
            ) : (
                <div className="card p-16 text-center">
                    <div className="w-16 h-16 rounded-2xl bg-slate-100 flex items-center justify-center mx-auto mb-4">
                        <i className="ri-run-line text-3xl text-slate-400" />
                    </div>
                    <h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100 mb-2">No exercises found</h3>
                    <p className="text-slate-500">Try adjusting your search criteria</p>
                </div>
            )}

            <Pagination
                currentPage={page}
                totalPages={totalPages}
                baseUrl={baseUrl}
                totalCount={totalCount}
                pageSize={PAGE_SIZE}
            />
        </div>
    );
}
