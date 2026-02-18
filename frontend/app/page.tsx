import Link from "next/link";
import Image from "next/image";
import placeHolderImage from "@/public/placeholder-recipe.jpg";
import DailyOverviewChart from "@/components/DailyOverviewChart";
import DashboardStats from "@/components/Dashboard/DashboardStats";
import { getUserOptionalServer } from "@/helper/session";
import { getPopularRecipes } from "@/data/recipe";
import { FeatureSection } from "@/components/Landing/FeatureSection";
import { TestimonialSection } from "@/components/Landing/TestimonialCard";
import { CTASection } from "@/components/Landing/CTASection";

export const dynamic = 'force-dynamic';

export default async function Home() {
	const user = await getUserOptionalServer();
	const popularRecipes = await getPopularRecipes();

	return (
		<div className="space-y-8">
			{/* Hero Section for non-authenticated users */}
			{!user && (
				<div data-testid="hero-section" className="relative overflow-hidden rounded-3xl bg-linear-to-br from-brand-600 via-brand-700 to-brand-800 p-8 sm:p-12" style={{ backgroundSize: '200% 200%', animation: 'gradient-shift 8s ease infinite' }}>
					<div className="absolute inset-0 bg-[url('/grid.svg')] opacity-10" />
					<div className="absolute top-0 right-0 w-96 h-96 bg-brand-400/30 rounded-full blur-3xl" />
					<div className="absolute bottom-0 left-0 w-64 h-64 bg-accent-500/20 rounded-full blur-3xl" />

					<div className="relative z-10 max-w-2xl">
						<div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-white/10 backdrop-blur-sm text-white/90 text-sm mb-6">
							<i className="ri-sparkling-2-fill text-amber-300" />
							AI-Powered Nutrition Coaching
						</div>
						<h1 className="text-4xl sm:text-5xl font-bold text-white mb-4 tracking-tight">
							Find Your <span className="text-amber-300">Balance</span>
						</h1>
						<p className="text-lg text-white/80 mb-8 leading-relaxed">
							Track your nutrition, plan meals, and achieve your fitness goals with personalized AI coaching. Mizan helps you find the perfect balance.
						</p>
						<div className="flex flex-col sm:flex-row gap-4">
							<Link
								href="/register"
								className="inline-flex items-center justify-center gap-2 px-6 py-3 bg-white dark:bg-slate-900 text-brand-700 dark:text-brand-400 font-semibold rounded-xl hover:bg-brand-50 dark:hover:bg-slate-800 transition-colors shadow-lg"
							>
								Get Started Free
								<i className="ri-arrow-right-line" />
							</Link>
							<Link
								href="/login"
								className="inline-flex items-center justify-center gap-2 px-6 py-3 bg-white/10 backdrop-blur-sm text-white font-semibold rounded-xl hover:bg-white/20 transition-colors border border-white/20"
							>
								Sign In
							</Link>
						</div>
					</div>
				</div>
			)}

			{/* Welcome back + Stats for authenticated users */}
			{user && (
				<div className="animate-in">
					<h2 className="text-2xl font-bold text-slate-900 dark:text-slate-100 mb-1">
						Welcome back{user.name ? `, ${user.name}` : ''}
					</h2>
					<p className="text-slate-500 dark:text-slate-400 text-sm mb-6">Here&apos;s your daily overview</p>
					<DashboardStats />
				</div>
			)}

			{/* Quick Actions */}
			<div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
				<Link href="/meals/add" className="group card-hover p-6">
					<div className="flex items-start gap-4">
						<div className="w-12 h-12 rounded-2xl bg-linear-to-br from-brand-400 to-brand-600 flex items-center justify-center shadow-lg shadow-brand-500/25 group-hover:shadow-brand-500/40 transition-shadow">
							<i className="ri-add-circle-line text-2xl text-white" />
						</div>
						<div>
							<h3 className="font-semibold text-slate-900 dark:text-slate-100 mb-1">Log Meal</h3>
							<p className="text-sm text-slate-500 dark:text-slate-400">Track what you eat</p>
						</div>
					</div>
				</Link>

				<Link href="/recipes/add" className="group card-hover p-6">
					<div className="flex items-start gap-4">
						<div className="w-12 h-12 rounded-2xl bg-linear-to-br from-accent-400 to-accent-600 flex items-center justify-center shadow-lg shadow-accent-500/25 group-hover:shadow-accent-500/40 transition-shadow">
							<i className="ri-restaurant-line text-2xl text-white" />
						</div>
						<div>
							<h3 className="font-semibold text-slate-900 dark:text-slate-100 mb-1">Create Recipe</h3>
							<p className="text-sm text-slate-500 dark:text-slate-400">Add your own recipes</p>
						</div>
					</div>
				</Link>

				<Link href="/suggestions" className="group card-hover p-6 sm:col-span-2 lg:col-span-1">
					<div className="flex items-start gap-4">
						<div className="w-12 h-12 rounded-2xl bg-linear-to-br from-violet-400 to-violet-600 flex items-center justify-center shadow-lg shadow-violet-500/25 group-hover:shadow-violet-500/40 transition-shadow">
							<i className="ri-magic-line text-2xl text-white" />
						</div>
						<div>
							<h3 className="font-semibold text-slate-900 dark:text-slate-100 mb-1">AI Coach</h3>
							<p className="text-sm text-slate-500 dark:text-slate-400">Get personalized advice</p>
						</div>
					</div>
				</Link>
			</div>

			{/* Feature Section (unauthenticated) */}
			{!user && <FeatureSection />}

			{/* Popular Recipes */}
			<div className="card p-6">
				<div className="flex items-center justify-between mb-6">
					<div>
						<h2 className="section-title">Popular Recipes</h2>
						<p className="text-sm text-slate-500 dark:text-slate-400 mt-1">Discover community favorites</p>
					</div>
					<Link href="/recipes" className="btn-secondary text-sm">
						View All
						<i className="ri-arrow-right-line" />
					</Link>
				</div>

				<div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
					{popularRecipes.length > 0 ? popularRecipes.map((recipe) => (
						<Link
							key={recipe._id.toString()}
							href={`/recipes/${recipe._id}`}
							className="group relative rounded-2xl overflow-hidden bg-slate-100 dark:bg-slate-800"
						>
							<Image
								src={placeHolderImage}
								alt={recipe.name}
								className="w-full h-48 object-cover group-hover:scale-105 transition-transform duration-300"
								width={400}
								height={300}
							/>
							<div className="absolute inset-0 bg-linear-to-t from-black/70 via-black/20 to-transparent" />
							<div className="absolute bottom-0 left-0 right-0 p-4">
								<h3 className="font-semibold text-white mb-1 line-clamp-1">{recipe.name}</h3>
								<div className="flex items-center gap-3 text-sm text-white/80">
									<span className="flex items-center gap-1">
										<i className="ri-fire-line" />
										{recipe.totalMacros.calories.toFixed()} kcal
									</span>
									<span className="flex items-center gap-1">
										<i className="ri-heart-pulse-line" />
										{recipe.totalMacros.protein.toFixed()}g protein
									</span>
								</div>
							</div>
						</Link>
					)) : (
						<div className="col-span-full text-center py-12 text-slate-500 dark:text-slate-400">
							<i className="ri-restaurant-line text-4xl mb-2 block opacity-50" />
							<p>No recipes yet. Be the first to add one!</p>
						</div>
					)}
				</div>
			</div>

			{/* Testimonials (unauthenticated) */}
			{!user && <TestimonialSection />}

			{/* Nutrition Overview */}
			{user && (
				<div className="card p-6">
					<div className="flex items-center justify-between mb-6">
						<div>
							<h2 className="section-title">Nutrition Overview</h2>
							<p className="text-sm text-slate-500 dark:text-slate-400 mt-1">Your daily progress</p>
						</div>
						<Link href="/goal" className="btn-primary text-sm">
							<i className="ri-target-line" />
							Set Goals
						</Link>
					</div>
					<DailyOverviewChart />
				</div>
			)}

			{/* CTA (unauthenticated) */}
			{!user && <CTASection />}
		</div>
	);
}
