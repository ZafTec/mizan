import Image from "next/image";
import Link from "next/link";
import { getExercises } from "@/data/exercise";
import { getUserServer } from "@/helper/session";
import Pagination from "@/components/Pagination";
import SearchExercises from "./SearchExercises";

export const dynamic = "force-dynamic";

const PAGE_SIZE = 20;

export default async function ExercisesPage({
  searchParams,
}: {
  searchParams: Promise<{ search?: string; category?: string; page?: string }>;
}) {
  await getUserServer();
  const params = await searchParams;
  const page = Math.max(1, parseInt(params.page || "1", 10) || 1);
  const { exercises, totalCount, totalPages } = await getExercises(
    params.search,
    params.category,
    page,
    PAGE_SIZE,
  );
  const filtered = Boolean(params.search || params.category);

  const baseUrl = `/exercises?${[
    params.search ? `search=${encodeURIComponent(params.search)}` : "",
    params.category ? `category=${encodeURIComponent(params.category)}` : "",
  ]
    .filter(Boolean)
    .join("&")}`;

  return (
    <div className="log-page space-y-8" data-testid="exercises-page">
      <header className="log-page-header block!">
        <h1>Exercises</h1>
        <p className="log-muted mt-2">
          The movements you can put in a workout.
        </p>
      </header>

      <SearchExercises
        initialSearch={params.search}
        initialCategory={params.category}
      />

      <section aria-labelledby="exercises-title">
        <header className="log-section-header">
          <h2 id="exercises-title">
            {filtered ? "Matching" : "All exercises"}
          </h2>
          <span className="num text-sm log-muted">{totalCount}</span>
        </header>
        {exercises.length === 0 ? (
          <div className="log-empty">
            <p>No exercises match.</p>
            {filtered && (
              <Link
                href="/exercises"
                className="text-link mt-3 inline-block text-sm"
              >
                Clear the search
              </Link>
            )}
          </div>
        ) : (
          <ul>
            {exercises.map((exercise) => (
              <li key={exercise.id} className="ledger-row">
                {exercise.imageUrl && (
                  <span className="relative h-11 w-11 shrink-0 overflow-hidden rounded-sm border border-border">
                    <Image
                      src={exercise.imageUrl}
                      alt=""
                      fill
                      sizes="44px"
                      className="object-cover"
                    />
                  </span>
                )}
                <span className="min-w-0 flex-1">
                  <span className="block font-medium">{exercise.name}</span>
                  <span className="mt-0.5 block text-sm log-muted">
                    {[
                      exercise.category,
                      exercise.muscleGroup,
                      exercise.equipment,
                    ]
                      .filter(Boolean)
                      .join(" · ")}
                  </span>
                </span>
                {exercise.videoUrl && (
                  <a
                    href={exercise.videoUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="text-link shrink-0 text-sm"
                  >
                    Video
                  </a>
                )}
              </li>
            ))}
          </ul>
        )}
      </section>

      <Pagination
        currentPage={page}
        totalPages={totalPages}
        baseUrl={baseUrl}
        totalCount={totalCount}
        pageSize={PAGE_SIZE}
      />

      <Link
        href="/workout/active"
        className="btn-secondary min-h-11 w-full sm:w-auto"
      >
        Start a workout
      </Link>
    </div>
  );
}
