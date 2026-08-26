import Link from "next/link";
import { getCurrentGoal } from "@/data/goal";
import { AnimatedIcon } from "@/components/ui/animated-icon";

/**
 * Tier 2 - docs/REFOCUS.md §3. Renders nothing once a goal exists, so it is a
 * one-time nudge rather than a permanent fixture. Setup is not a nav item for
 * the same reason: nobody needs a standing link to something they do once.
 */
export default async function SetupPrompt() {
	const goal = await getCurrentGoal();
	if (goal) return null;

	return (
		<Link
			href="/onboarding"
			className="flex items-center gap-3 rounded-3xl border border-brand-500/30 bg-brand-500/10 p-4 transition-colors hover:border-brand-500/50"
		>
			<span className="icon-chip h-10 w-10 shrink-0 text-brand-600 dark:text-brand-400">
				<AnimatedIcon name="sparkles" size={18} />
			</span>
			<div className="min-w-0 flex-1">
				<p className="text-sm font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
					You have not set a target yet
				</p>
				<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
					A couple of minutes of conversation and the numbers on this page mean
					something
				</p>
			</div>
			<span className="btn-primary shrink-0 !rounded-2xl !py-2 text-xs">
				Set up
				<AnimatedIcon name="arrowRight" size={14} />
			</span>
		</Link>
	);
}
