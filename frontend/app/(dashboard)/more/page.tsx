import type { Metadata } from "next";
import { getUserOptionalServer } from "@/helper/session";
import { visibleGroups } from "@/components/Layout/nav";
import MoreDirectory from "./MoreDirectory";

export const metadata: Metadata = {
	title: "More · Mizan",
};

/**
 * Tier 3 - see docs/REFOCUS.md §3.
 *
 * Everything the product does that is not logging. One tap from the spine,
 * zero permanent pixels until asked for. The list itself is filtered client
 * side; which items exist at all still depends on the role resolved here.
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

			<MoreDirectory groups={groups} />
		</div>
	);
}
