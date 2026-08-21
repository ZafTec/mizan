import Link from "next/link";
import { getActiveWorkoutDraft } from "@/data/workout";
import { AnimatedIcon } from "@/components/ui/animated-icon";

function relativeTime(iso: string): string {
	const minutes = Math.round((Date.now() - Date.parse(iso)) / 60000);
	if (!Number.isFinite(minutes) || minutes < 1) return "just now";
	if (minutes < 60) return `${minutes} min ago`;
	const hours = Math.round(minutes / 60);
	if (hours < 24) return `${hours}h ago`;
	const days = Math.round(hours / 24);
	return `${days}d ago`;
}

/**
 * Tier 2 - docs/REFOCUS.md §3. Renders nothing unless a session is open, which
 * is why it is safe to mount unconditionally on the spine.
 */
export default async function ResumeWorkoutBanner() {
	const draft = await getActiveWorkoutDraft();
	if (!draft) return null;

	const progress =
		draft.totalSets > 0
			? `${draft.completedSets}/${draft.totalSets} sets`
			: `${draft.exerciseCount} exercise${draft.exerciseCount === 1 ? "" : "s"}`;

	return (
		<Link
			href="/workouts"
			className="flex items-center gap-3 rounded-3xl border border-verdigris-500/30 bg-verdigris-500/10 p-4 transition-colors hover:border-verdigris-500/50 dark:bg-verdigris-500/10"
		>
			<span className="icon-chip h-10 w-10 shrink-0 text-verdigris-700 dark:text-verdigris-300">
				<AnimatedIcon name="activity" size={18} />
			</span>
			<div className="min-w-0 flex-1">
				<p className="truncate text-sm font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
					{draft.name} in progress
				</p>
				<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
					{progress} · saved {relativeTime(draft.updatedAt)}
				</p>
			</div>
			<span className="btn-primary shrink-0 !rounded-2xl !py-2 text-xs">
				Resume
				<AnimatedIcon name="arrowRight" size={14} />
			</span>
		</Link>
	);
}
