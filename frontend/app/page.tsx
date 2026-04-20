import Link from "next/link";
import Image from "next/image";
import { redirect } from "next/navigation";
import placeHolderImage from "@/public/placeholder-recipe.jpg";
import DailyOverviewChart from "@/components/DailyOverviewChart";
import { AnimatedIcon, type AnimatedIconName } from "@/components/ui/animated-icon";
import { getUserOptionalServer } from "@/helper/session";
import { getPopularRecipes } from "@/data/recipe";
import { FeatureSection } from "@/components/Landing/FeatureSection";
import { TestimonialSection } from "@/components/Landing/TestimonialCard";
import { CTASection } from "@/components/Landing/CTASection";

export const dynamic = 'force-dynamic';

const sectionHeadingClass = "text-2xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-3xl";
const sectionBodyClass = "mt-2 text-sm leading-6 text-charcoal-blue-700 dark:text-charcoal-blue-300";
const cardTitleClass = "mb-1 font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50";
const cardBodyClass = "text-sm leading-6 text-charcoal-blue-700 dark:text-charcoal-blue-300";

const quickActions: Array<{
	href: string;
	title: string;
	description: string;
	icon: AnimatedIconName;
	iconClass: string;
}> = [
	{
		href: "/meals/add",
		title: "Log Meal",
		description: "Capture meals fast without breaking your flow.",
		icon: "flame",
		iconClass: "bg-brand-600",
	},
	{
		href: "/recipes/add",
		title: "Create Recipe",
		description: "Build reusable dishes with macros already calculated.",
		icon: "cookingPot",
		iconClass: "bg-accent-600",
	},
	{
		href: "/suggestions",
		title: "AI Coach",
		description: "Get suggestions that match goals, habits, and constraints.",
		icon: "brain",
		iconClass: "bg-charcoal-blue-900 dark:bg-charcoal-blue-100 dark:text-charcoal-blue-900",
	},
];

export default async function Home() {
	const user = await getUserOptionalServer();

	if (user) {
		redirect("/dashboard");
	}

	const popularRecipes = await getPopularRecipes();

	return (
		<div className="space-y-10">
			{!user && (
				<div
					data-testid="hero-section"
					className="surface-panel relative overflow-hidden rounded-[34px] border-charcoal-blue-900 bg-charcoal-blue-900 p-8 sm:p-10 lg:p-12 dark:border-white/10 dark:bg-charcoal-blue-950"
				>
					<div className="absolute inset-0 bg-[url('/grid.svg')] opacity-10" />
					<div className="absolute right-0 top-0 h-96 w-96 rounded-full bg-brand-500/10 blur-3xl" />
					<div className="absolute bottom-0 left-0 h-64 w-64 rounded-full bg-white/5 blur-3xl" />
					<div className="absolute right-0 top-1/2 -translate-y-1/2 hidden lg:block w-112.5 h-112.5 opacity-90 xl:right-10 2xl:right-20 pointer-events-none">
						<Image src="/assets/hero-device.svg" alt="Mizan App Preview" fill className="object-contain drop-shadow-2xl" />
					</div>

					<div className="relative z-10 max-w-3xl">
						<h1 className="max-w-2xl text-4xl font-semibold tracking-tight text-black dark:text-white sm:text-5xl lg:text-6xl">
							Find Your <span className="text-brand-400 dark:text-brand-300">Balance</span> Across Food, Training, and Progress.
						</h1>
						<p className="mt-5 max-w-2xl text-lg leading-relaxed text-black/90 dark:text-white/90 sm:text-xl">
							Track your nutrition, plan meals, and achieve your fitness goals with personalized AI coaching. Mizan helps you find the perfect balance.
						</p>
						<div className="mt-8 flex flex-col gap-4 sm:flex-row">
							<Link href="/register" className="btn-secondary btn-lg border-white/20 bg-white/95 dark:bg-white/95 text-brand-600 dark:text-brand-600 hover:bg-white dark:hover:bg-white">
								Get Started Free
								<AnimatedIcon name="arrowRight" size={18} aria-hidden="true" />
							</Link>
							<Link
								href="/login"
								className="btn-lg inline-flex items-center justify-center gap-2 rounded-2xl border border-white/20 dark:border-white/10 bg-white/10 dark:bg-white/10 px-6 py-3.5 font-semibold text-white backdrop-blur-sm transition-colors hover:bg-white/20 dark:hover:bg-white/20"
							>
								<AnimatedIcon name="lock" size={18} aria-hidden="true" />
								Sign In
							</Link>
						</div>

						<div className="mt-8 grid grid-cols-1 gap-3 sm:grid-cols-3">
							{["Fast logging", "Smart planning", "Trainer-ready progress"].map((item, index) => (
								<div key={item} className="stagger-item rounded-2xl border border-white/20 bg-white/10 px-4 py-3 text-sm text-white/90 backdrop-blur-sm" style={{ animationDelay: `${index * 100}ms` }}>
									{item}
								</div>
							))}
						</div>
					</div>
				</div>
			)}

			<div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
				{quickActions.map((action, index) => (
					<Link key={action.href} href={action.href} className="card-hover stagger-item group p-6 sm:p-7" style={{ animationDelay: `${index * 90}ms` }}>
						<div className="flex items-start gap-4">
							<div className={`flex h-12 w-12 items-center justify-center rounded-2xl ${action.iconClass} text-white shadow-lg shadow-charcoal-blue-950/80 dark:shadow-charcoal-blue-950/10`}>
								<AnimatedIcon name={action.icon} size={20} aria-hidden="true" />
							</div>
							<div className="space-y-1">
								<h3 className={cardTitleClass}>{action.title}</h3>
								<p className={cardBodyClass}>{action.description}</p>
							</div>
						</div>
					</Link>
				))}
			</div>

			{!user && <FeatureSection />}

			<div className="surface-panel p-6 sm:p-8">
				<div className="mb-6 flex items-center justify-between gap-4">
					<div>
						<h2 className={sectionHeadingClass}>Popular Recipes</h2>
						<p className={sectionBodyClass}>Discover community favorites</p>
					</div>
					<Link href="/recipes" className="btn-secondary btn-sm">
						View All
						<AnimatedIcon name="arrowRight" size={16} aria-hidden="true" />
					</Link>
				</div>

				<div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
					{popularRecipes.length > 0 ? (
						popularRecipes.map((recipe) => (
							<Link
								key={recipe._id.toString()}
								href={`/recipes/${recipe._id}`}
								className="group relative overflow-hidden rounded-[24px] border border-charcoal-blue-200 bg-charcoal-blue-50 shadow-lg shadow-charcoal-blue-950/5 dark:border-white/10 dark:bg-charcoal-blue-900/60"
							>
								<Image
									src={recipe.imageUrl || placeHolderImage}
									alt={recipe.name}
									className="h-48 w-full object-cover transition-transform duration-300 group-hover:scale-105"
									width={400}
									height={300}
								/>
								<div className="absolute inset-0 bg-linear-to-t from-black/70 via-black/20 to-transparent" />
								<div className="absolute bottom-0 left-0 right-0 p-4">
									<h3 className="mb-1 line-clamp-1 font-semibold text-white drop-shadow-md">{recipe.name}</h3>
									<div className="flex items-center gap-3 text-sm text-white/90 drop-shadow-md">
										<span className="flex items-center gap-1">
											<i className="ri-fire-line" aria-hidden="true" />
											{recipe.totalMacros.calories.toFixed()} kcal
										</span>
										<span className="flex items-center gap-1">
											<i className="ri-heart-pulse-line" aria-hidden="true" />
											{recipe.totalMacros.protein.toFixed()}g protein
										</span>
									</div>
								</div>
							</Link>
						))
					) : (
						<div className="col-span-full py-12 text-center text-charcoal-blue-700 dark:text-charcoal-blue-300">
							<i className="ri-restaurant-line mb-2 block text-4xl text-charcoal-blue-400 dark:text-charcoal-blue-500" aria-hidden="true" />
							<p>No recipes yet. Be the first to add one!</p>
						</div>
					)}
				</div>
			</div>

			{!user && <TestimonialSection />}

			{user && (
				<div className="surface-panel p-6 sm:p-8">
					<div className="mb-6 flex items-center justify-between gap-4">
						<div>
							<h2 className={sectionHeadingClass}>Nutrition Overview</h2>
							<p className={sectionBodyClass}>Your daily progress</p>
						</div>
						<Link href="/goal" className="btn-primary btn-sm">
							<AnimatedIcon name="rocket" size={16} aria-hidden="true" />
							Set Goals
						</Link>
					</div>
					<DailyOverviewChart />
				</div>
			)}

			{!user && <CTASection />}
		</div>
	);
}
