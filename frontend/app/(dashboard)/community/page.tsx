import Link from "next/link";
import Image from "next/image";
import { AnimatedIcon } from "@/components/ui/animated-icon";
import { getPopularRecipes } from "@/data/recipe";
import placeholder from "@/public/placeholder-recipe.jpg";

export const dynamic = "force-dynamic";

function macroChipValue(label: string, value: number, tone: "protein" | "carbs" | "fat" | "calories") {
	return (
		<span
			className={`macro-chip-${tone} rounded-full px-2 py-0.5 text-[10px] font-semibold uppercase tracking-[0.1em]`}
		>
			{Math.round(value)}
			<span className="ml-0.5 normal-case tracking-normal text-[9px]">{label}</span>
		</span>
	);
}

export default async function CommunityPage() {
	const popular = await getPopularRecipes();

	return (
		<div className="space-y-6 lg:space-y-8">
			<header className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
				<div className="space-y-2">
					<p className="eyebrow">People of Mizan</p>
					<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
						Community
					</h1>
					<p className="max-w-2xl text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
						See what the community is cooking, climb the consistency leaderboard, and join challenges.
					</p>
				</div>
			</header>

			<section className="glass-panel relative overflow-hidden p-6 sm:p-8">
				<div className="absolute right-[-10%] top-[-30%] h-64 w-64 rounded-full bg-sandy-brown-300/30 blur-3xl" />
				<div className="relative grid gap-6 sm:grid-cols-[1fr_auto] sm:items-end">
					<div className="space-y-3">
						<h2 className="text-2xl font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
							Join the community conversation
						</h2>
						<p className="max-w-xl text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
							Trade recipes, swap progress notes, and celebrate wins. Community feeds, groups and challenges roll out with the v2 release.
						</p>
						<div className="flex flex-wrap gap-2 pt-1">
							<Link href="/recipes" className="btn-primary !rounded-2xl !py-2 text-sm">
								Browse recipes
								<AnimatedIcon name="cookingPot" size={14} />
							</Link>
							<Link href="/achievements" className="btn-ghost !rounded-2xl !py-2 text-sm">
								Your achievements
								<AnimatedIcon name="sparkles" size={14} />
							</Link>
						</div>
					</div>
					<div className="hidden gap-2 sm:flex">
						<span className="icon-chip h-14 w-14 text-verdigris-700 dark:text-verdigris-300">
							<AnimatedIcon name="users" size={22} />
						</span>
					</div>
				</div>
			</section>

			<section>
				<div className="mb-4 flex items-end justify-between">
					<div>
						<h2 className="text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
							Popular recipes this week
						</h2>
						<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
							Community-favourited dishes sorted by saves.
						</p>
					</div>
					<Link href="/recipes" className="btn-ghost !rounded-2xl !py-2 text-sm">
						Browse all
						<AnimatedIcon name="arrowRight" size={14} />
					</Link>
				</div>

				{popular.length === 0 ? (
					<div className="glass-panel flex flex-col items-center gap-2 p-10 text-center">
						<span className="icon-chip h-12 w-12 text-charcoal-blue-400">
							<AnimatedIcon name="cookingPot" size={18} />
						</span>
						<p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
							Nothing trending yet. Be the first to publish a recipe.
						</p>
					</div>
				) : (
					<div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
						{popular.slice(0, 6).map((recipe) => (
							<Link
								key={recipe._id}
								href={`/recipes/${recipe._id}`}
								className="glass-panel group flex flex-col overflow-hidden p-0 transition-all hover:-translate-y-1"
							>
								<div className="relative aspect-[16/10] w-full overflow-hidden">
									<Image
										src={recipe.imageUrl || placeholder}
										alt={recipe.name}
										fill
										sizes="(max-width: 768px) 100vw, (max-width: 1024px) 50vw, 33vw"
										className="object-cover transition-transform group-hover:scale-[1.03]"
									/>
								</div>
								<div className="space-y-3 p-5">
									<h3 className="text-base font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
										{recipe.name}
									</h3>
									<div className="flex flex-wrap gap-1.5">
										{macroChipValue("kcal", recipe.totalMacros.calories, "calories")}
										{macroChipValue("p", recipe.totalMacros.protein, "protein")}
										{macroChipValue("c", recipe.totalMacros.carbs, "carbs")}
										{macroChipValue("f", recipe.totalMacros.fat, "fat")}
									</div>
								</div>
							</Link>
						))}
					</div>
				)}
			</section>

			<section className="grid gap-4 md:grid-cols-2">
				<div className="glass-panel p-6">
					<div className="mb-4 flex items-center gap-3">
						<span className="icon-chip h-10 w-10 text-tuscan-sun-700 dark:text-tuscan-sun-300">
							<AnimatedIcon name="flame" size={18} />
						</span>
						<div>
							<h2 className="text-sm font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
								Challenges
							</h2>
							<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
								Opt-in consistency streaks
							</p>
						</div>
					</div>
					<p className="text-sm text-charcoal-blue-600 dark:text-charcoal-blue-300">
						Weekly logging, protein-target and workout consistency challenges arrive with the next release. Track your solo streak on the <Link href="/dashboard" className="text-verdigris-700 underline underline-offset-2 dark:text-verdigris-300">dashboard</Link> until then.
					</p>
				</div>
				<div className="glass-panel p-6">
					<div className="mb-4 flex items-center gap-3">
						<span className="icon-chip h-10 w-10 text-sandy-brown-700 dark:text-sandy-brown-300">
							<AnimatedIcon name="users" size={18} />
						</span>
						<div>
							<h2 className="text-sm font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
								Groups
							</h2>
							<p className="text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400">
								Invite-only accountability pods
							</p>
						</div>
					</div>
					<p className="text-sm text-charcoal-blue-600 dark:text-charcoal-blue-300">
						Household-sized groups and coach-led pods are in design. Want to beta-test? Ping your coach in <Link href="/messaging" className="text-verdigris-700 underline underline-offset-2 dark:text-verdigris-300">messaging</Link>.
					</p>
				</div>
			</section>
		</div>
	);
}
