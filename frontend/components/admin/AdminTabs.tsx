"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { Icon } from "@/components/ui/icon";
import { ADMIN_TABS } from "@/components/Layout/nav";
import { cn } from "@/lib/utils";

/**
 * Persistent tab strip for every /admin section. Replaces the dashboard's
 * "Quick Actions" grid, which only ever linked to half of them - the rest
 * (moderation, achievements, relationships, audit log, the AI console, jobs)
 * had no way in short of typing the URL.
 */
export default function AdminTabs() {
	const pathname = usePathname();

	return (
		<div className="-mx-4 overflow-x-auto border-b border-charcoal-blue-200 px-4 sm:mx-0 sm:px-0 dark:border-charcoal-blue-700">
			<div className="flex min-w-max gap-1 pb-px">
				{ADMIN_TABS.map((tab) => {
					const active = tab.href === "/admin" ? pathname === "/admin" : pathname?.startsWith(tab.href);
					return (
						<Link
							key={tab.href}
							href={tab.href}
							className={cn(
								"press-feedback flex items-center gap-2 whitespace-nowrap border-b-2 px-3 py-2.5 text-sm font-medium transition-colors",
								active
									? "border-charcoal-blue-900 text-charcoal-blue-900 dark:border-charcoal-blue-50 dark:text-charcoal-blue-50"
									: "border-transparent text-charcoal-blue-500 hover:text-charcoal-blue-800 dark:text-charcoal-blue-400 dark:hover:text-charcoal-blue-100",
							)}
						>
							<Icon name={tab.icon} size={15} aria-hidden="true" />
							{tab.label}
						</Link>
					);
				})}
			</div>
		</div>
	);
}
