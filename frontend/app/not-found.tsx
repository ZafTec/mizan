import Link from "next/link";

export default function NotFound() {
	return (
		<div className="min-h-[70vh] flex items-center justify-center px-4">
			<div className="w-full max-w-md">
				{/* Header */}
				<div className="text-center mb-8">
					<div className="inline-flex items-center justify-center w-20 h-20 rounded-2xl bg-linear-to-br from-slate-400 to-slate-600 shadow-lg shadow-slate-500/30 mb-6">
						<i className="ri-error-warning-line text-4xl text-white" />
					</div>
					<h1 className="text-6xl font-bold text-slate-900 dark:text-slate-100 mb-2">404</h1>
					<h2 className="text-2xl font-bold text-slate-700 dark:text-slate-300 mb-2">Page not found</h2>
					<p className="text-slate-500 dark:text-slate-400">
						Sorry, we couldn&apos;t find the page you&apos;re looking for.
					</p>
				</div>

				{/* Action Card */}
				<div className="card p-6 sm:p-8 space-y-4">
					<div className="space-y-3">
						<Link href="/" className="btn-primary w-full py-3">
							<i className="ri-home-line" />
							Go to Dashboard
						</Link>
						<Link href="/recipes" className="btn-secondary w-full py-3">
							<i className="ri-restaurant-line" />
							Browse Recipes
						</Link>
					</div>

					<div className="pt-4 border-t border-slate-200 dark:border-slate-700">
						<p className="text-sm text-slate-500 dark:text-slate-400 text-center mb-3">
							Need help? Try these popular pages:
						</p>
						<div className="grid grid-cols-2 gap-2 text-sm">
							<Link
								href="/meals"
								className="text-brand-600 dark:text-brand-400 hover:text-brand-700 dark:hover:text-brand-300 hover:underline text-center"
							>
								Meals
							</Link>
							<Link
								href="/ingredients"
								className="text-brand-600 dark:text-brand-400 hover:text-brand-700 dark:hover:text-brand-300 hover:underline text-center"
							>
								Ingredients
							</Link>
							<Link
								href="/goal"
								className="text-brand-600 dark:text-brand-400 hover:text-brand-700 dark:hover:text-brand-300 hover:underline text-center"
							>
								Goals
							</Link>
							<Link
								href="/profile"
								className="text-brand-600 dark:text-brand-400 hover:text-brand-700 dark:hover:text-brand-300 hover:underline text-center"
							>
								Profile
							</Link>
						</div>
					</div>
				</div>
			</div>
		</div>
	);
}
