import { getExercises } from "@/data/exercise";
import { getUserServer } from "@/helper/session";
import SearchExercises from "./SearchExercises";
import Image from "next/image";

export const dynamic = 'force-dynamic';

export default async function ExercisesPage({
    searchParams,
}: {
    searchParams: Promise<{ search?: string; category?: string }>;
}) {
    await getUserServer();
    const params = await searchParams;
    const { exercises, totalCount } = await getExercises(params.search, params.category);

    return (
        <div className="space-y-8">
            <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
                <div>
                    <h1 className="text-2xl font-bold text-slate-900">Exercises</h1>
                    <p className="text-slate-500 mt-1">Browse {totalCount} exercises for your workouts</p>
                </div>
            </div>

            <SearchExercises initialSearch={params.search} initialCategory={params.category} />

            {exercises.length > 0 ? (
                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                    {exercises.map((exercise) => (
                        <div key={exercise.id} className="card-hover p-5">
                            <div className="flex items-start gap-4">
                                <div className="w-12 h-12 rounded-2xl bg-linear-to-br from-brand-400 to-brand-600 flex items-center justify-center shadow-lg shrink-0 relative overflow-hidden">
                                    {exercise.imageUrl ? (
                                        <Image
                                            src={exercise.imageUrl}
                                            alt={exercise.name}
                                            fill
                                            className="object-cover rounded-2xl"
                                            unoptimized
                                        />
                                    ) : (
                                        <i className="ri-run-line text-xl text-white" />
                                    )}
                                </div>
                                <div className="flex-1 min-w-0">
                                    <h3 className="font-semibold text-slate-900 truncate">
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
                    <h3 className="text-lg font-semibold text-slate-900 mb-2">No exercises found</h3>
                    <p className="text-slate-500">Try adjusting your search criteria</p>
                </div>
            )}
        </div>
    );
}
