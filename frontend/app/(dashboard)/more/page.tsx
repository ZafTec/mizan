import Link from "next/link";
import type { Metadata } from "next";
import { getUserOptionalServer } from "@/helper/session";
import { Icon } from "@/components/ui/icon";
import { visibleGroups } from "@/components/Layout/nav";

export const metadata: Metadata = {
	title: "More · Mizan",
};

/**
 * Tier 3 - see docs/REFOCUS.md §3.
 *
 * Everything the product does that is not logging. One tap from the spine,
 * zero permanent pixels until asked for.
 */
export default async function MorePage() {
	const user = await getUserOptionalServer();
	const groups = visibleGroups(user?.role === "admin");

	return (
		<div className="space-y-8">
			<header className="space-y-1">
				<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
					More
				</h1>
				<p className="text-charcoal-blue-500 dark:text-charcoal-blue-400">
					Everything beyond your daily log.
				</p>
			</header>

			{groups.map((group) => (
				<section key={group.label} className="space-y-3">
					<h2 className="text-[11px] font-semibold uppercase tracking-[0.18em] text-charcoal-blue-400">
						{group.label}
					</h2>
					<div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
						{group.items.map((item) => (
							<Link
								key={item.href}
								href={item.href}
								className="surface-panel flex items-start gap-3 p-4 transition-colors hover:border-brand-500/40 hover:bg-brand-50/40 dark:hover:bg-white/5"
							>
								<span className="icon-chip h-10 w-10 shrink-0 text-brand-600 dark:text-brand-400">
									<Icon name={item.icon} size={18} />
								</span>
								<span className="min-w-0">
									<span className="block font-medium text-charcoal-blue-900 dark:text-charcoal-blue-50">
										{item.label}
									</span>
									{item.description && (
										<span className="block text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
											{item.description}
										</span>
									)}
								</span>
							</Link>
						))}
					</div>
				</section>
			))}
		</div>
	);
}
