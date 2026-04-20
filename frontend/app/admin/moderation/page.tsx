import Link from "next/link";
import { AnimatedIcon, type AnimatedIconName } from "@/components/ui/animated-icon";
import { getAllIngredient } from "@/data/ingredient";
import { getAllRecipes } from "@/data/recipe";
import { getAuditLogs } from "@/data/audit";

export const dynamic = "force-dynamic";

async function loadSignals() {
	const [ingredientData, recipeData, auditData] = await Promise.all([
		getAllIngredient(undefined, "createdAt", "desc", 1, 1).catch(() => ({ totalCount: 0 })),
		getAllRecipes(undefined, 1, 1).catch(() => ({ totalCount: 0 })),
		getAuditLogs({ page: 1, pageSize: 5 }).catch(() => ({ logs: [], totalCount: 0, totalPages: 0 })),
	]);
	return {
		ingredientCount: ingredientData.totalCount ?? 0,
		recipeCount: recipeData.totalCount ?? 0,
		auditRecent: auditData.logs ?? [],
		auditTotal: auditData.totalCount ?? 0,
	};
}

type QueueTile = {
	href: string;
	label: string;
	description: string;
	icon: AnimatedIconName;
	total: number;
	tone: "peach" | "brand" | "sun" | "sand";
};

const toneClass: Record<QueueTile["tone"], string> = {
	brand: "text-verdigris-700 dark:text-verdigris-300 [&_.qa-icon]:bg-verdigris-500/15",
	sand: "text-sandy-brown-700 dark:text-sandy-brown-300 [&_.qa-icon]:bg-sandy-brown-500/15",
	peach: "text-burnt-peach-700 dark:text-burnt-peach-300 [&_.qa-icon]:bg-burnt-peach-500/15",
	sun: "text-tuscan-sun-700 dark:text-tuscan-sun-300 [&_.qa-icon]:bg-tuscan-sun-500/15",
};

export default async function AdminModerationPage() {
	const signals = await loadSignals();

	const tiles: QueueTile[] = [
		{
			href: "/admin/ingredients",
			label: "Foods catalogue",
			description: "Review user-submitted foods, correct macros, flag issues.",
			icon: "search",
			total: signals.ingredientCount,
			tone: "peach",
		},
		{
			href: "/admin/recipes",
			label: "Recipes",
			description: "Moderate public recipes and their ingredient pairings.",
			icon: "cookingPot",
			total: signals.recipeCount,
			tone: "brand",
		},
		{
			href: "/admin/exercises",
			label: "Exercises",
			description: "Moderate the exercise library, categorisation and cues.",
			icon: "activity",
			total: 0,
			tone: "sand",
		},
		{
			href: "/admin/users",
			label: "Users",
			description: "Suspend, promote, verify or delete user accounts.",
			icon: "users",
			total: 0,
			tone: "sun",
		},
	];

	return (
		<div className="space-y-6 lg:space-y-8">
			<header className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
				<div className="space-y-2">
					<p className="eyebrow">Moderation</p>
					<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
						Review queue
					</h1>
					<p className="max-w-2xl text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
						One stop for content and account moderation. Dive into a queue, or skim recent audit-log activity.
					</p>
				</div>
				<Link href="/admin" className="btn-ghost !rounded-2xl !py-2 text-sm">
					Admin home
					<AnimatedIcon name="arrowRight" size={14} />
				</Link>
			</header>

			<section className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
				{tiles.map((t) => (
					<Link
						key={t.href}
						href={t.href}
						className={`glass-panel group flex flex-col gap-3 p-5 transition-all hover:-translate-y-1 ${toneClass[t.tone]}`}
					>
						<div className="flex items-start justify-between gap-3">
							<span className="qa-icon flex h-11 w-11 items-center justify-center rounded-2xl">
								<AnimatedIcon name={t.icon} size={18} />
							</span>
							<span className="rounded-full border border-charcoal-blue-200 bg-white/60 px-2 py-0.5 text-[10px] font-semibold text-charcoal-blue-600 dark:border-white/10 dark:bg-charcoal-blue-950/60 dark:text-charcoal-blue-300">
								{t.total.toLocaleString()}
							</span>
						</div>
						<div>
							<p className="text-sm font-semibold">{t.label}</p>
							<p className="mt-1 text-[11px] leading-relaxed text-charcoal-blue-500 dark:text-charcoal-blue-400">
								{t.description}
							</p>
						</div>
					</Link>
				))}
			</section>

			<section className="grid gap-4 lg:grid-cols-[1.5fr_1fr]">
				<div className="glass-panel p-6">
					<header className="mb-4 flex items-center justify-between">
						<div>
							<h2 className="text-base font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
								Recent audit activity
							</h2>
							<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
								{signals.auditTotal.toLocaleString()} logged events
							</p>
						</div>
						<Link href="/admin/audit-logs" className="btn-ghost !rounded-2xl !py-2 text-sm">
							Open full log
							<AnimatedIcon name="arrowRight" size={14} />
						</Link>
					</header>

					{signals.auditRecent.length === 0 ? (
						<p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
							No recent audit entries.
						</p>
					) : (
						<ul className="space-y-2">
							{signals.auditRecent.map((log) => (
								<li
									key={log.id}
									className="flex items-start gap-3 rounded-2xl border border-charcoal-blue-200 bg-white/70 p-3 text-sm dark:border-white/10 dark:bg-charcoal-blue-950/60"
								>
									<span className="icon-chip h-8 w-8 text-verdigris-700 dark:text-verdigris-300">
										<AnimatedIcon name="shieldCheck" size={14} />
									</span>
									<div className="min-w-0 flex-1">
										<p className="truncate text-sm text-charcoal-blue-900 dark:text-charcoal-blue-50">
											{log.action}
											<span className="ml-1 text-charcoal-blue-400">· {log.entityType}</span>
										</p>
										<p className="text-[11px] text-charcoal-blue-500 dark:text-charcoal-blue-400">
											{log.userEmail ?? log.userId ?? "system"}
											{log.timestamp ? ` · ${new Date(log.timestamp).toLocaleString()}` : ""}
										</p>
									</div>
								</li>
							))}
						</ul>
					)}
				</div>

				<div className="glass-panel p-6">
					<header className="mb-3">
						<h2 className="text-base font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
							Shortcuts
						</h2>
						<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
							Frequent admin jumps
						</p>
					</header>
					<div className="grid gap-2">
						<Link href="/admin/relationships" className="btn-ghost !rounded-2xl !py-2 text-sm justify-start">
							<AnimatedIcon name="users" size={14} /> Trainer–client relationships
						</Link>
						<Link href="/admin/sessions" className="btn-ghost !rounded-2xl !py-2 text-sm justify-start">
							<AnimatedIcon name="lock" size={14} /> Active sessions
						</Link>
						<Link href="/admin/users/create" className="btn-ghost !rounded-2xl !py-2 text-sm justify-start">
							<AnimatedIcon name="user" size={14} /> Create user
						</Link>
						<Link href="/admin/ingredients/add" className="btn-ghost !rounded-2xl !py-2 text-sm justify-start">
							<AnimatedIcon name="upload" size={14} /> Add ingredient
						</Link>
					</div>
				</div>
			</section>
		</div>
	);
}
