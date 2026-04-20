import Link from "next/link";
import { AnimatedIcon } from "@/components/ui/animated-icon";

export const dynamic = "force-dynamic";

export default function NotificationsPage() {
	return (
		<div className="space-y-6">
			<header className="flex flex-col gap-2">
				<p className="eyebrow">Inbox</p>
				<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50">
					Notifications
				</h1>
				<p className="max-w-2xl text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
					Stay on top of trainer replies, streak reminders, new achievements, and meal-plan updates.
				</p>
			</header>

			<section className="glass-panel flex flex-col items-center justify-center gap-4 p-12 text-center">
				<span className="icon-chip h-14 w-14 text-verdigris-700 dark:text-verdigris-300">
					<AnimatedIcon name="bell" size={24} />
				</span>
				<div className="space-y-1">
					<p className="text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
						All caught up
					</p>
					<p className="max-w-md text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
						The full notification center (trainer messages, goal pings, community activity, and AI coach nudges) lands with the v2 rollout. This is the placeholder.
					</p>
				</div>
				<Link href="/dashboard" className="btn-primary !rounded-2xl !py-2 text-sm">
					Back to dashboard
					<AnimatedIcon name="arrowRight" size={14} />
				</Link>
			</section>
		</div>
	);
}
