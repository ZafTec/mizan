"use client";

import Link from 'next/link';
import Image from 'next/image';
import placeHolderImage from '@/public/placeholder-recipe.jpg';
import type { SuggestedRecipe } from '@/data/suggestion';

interface SuggestedRecipesProps {
  user?: {
    id: string;
    name: string;
    email: string;
  } | null;
  suggestions?: SuggestedRecipe[];
  serverError?: string;
}

export default function SuggestedRecipes({ user, suggestions, serverError }: SuggestedRecipesProps) {

	if (serverError) {
		return (
		<div className="bg-red-50 dark:bg-red-950 border border-red-200 dark:border-red-800 rounded-lg p-4 mb-6">
			<p className="text-red-600 dark:text-red-400">{serverError}</p>
		</div>
		);
	}

	return (
		<div className="space-y-6">
			{/* Page Header */}
			<div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
				<div>
					<h1 className="text-2xl font-bold text-slate-900 dark:text-slate-100">Recipe Suggestions</h1>
					<p className="text-slate-500 dark:text-slate-400 mt-1">Personalized recipes based on your remaining macros</p>
				</div>
				{user && (
					<Link href="/suggestions/regenerate" className="btn-secondary">
						<i className="ri-refresh-line" />
						Regenerate
					</Link>
				)}
			</div>

			{!suggestions || suggestions.length === 0 ? (
				<div className="card p-8 text-center">
					<div className="w-16 h-16 rounded-2xl bg-slate-100 dark:bg-slate-800 flex items-center justify-center mx-auto mb-4">
						<i className="ri-lightbulb-line text-3xl text-slate-400 dark:text-slate-500" />
					</div>
					<h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100 mb-2">No Suggestions Available</h3>
					<p className="text-slate-500 dark:text-slate-400 mb-4">
						Add more meals or update your preferences to get personalized recipe suggestions.
					</p>
					<Link href="/meals" className="btn-primary">
						<i className="ri-add-line" />
						Log a Meal
					</Link>
				</div>
			) : (
				<div className="space-y-4">
					{suggestions.map((recipe) => (
						<Link
							key={recipe.id}
							href={`/recipes/${recipe.id}`}
							className="card-hover flex flex-col sm:flex-row overflow-hidden group"
						>
							<div className="sm:w-48 h-48 sm:h-auto bg-slate-200 dark:bg-slate-700 relative">
								<Image
									src={recipe.imageUrl || placeHolderImage}
									alt={recipe.title}
									fill
									className="object-cover group-hover:scale-105 transition-transform duration-300"
								/>
							</div>
							<div className="flex-1 p-6">
								<div className="mb-4">
									<h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100 group-hover:text-brand-600 dark:group-hover:text-brand-400 transition-colors mb-2">
										{recipe.title}
									</h3>
									<p className="text-slate-600 dark:text-slate-400 text-sm line-clamp-2">
										{recipe.description || recipe.reason}
									</p>
								</div>
								<div className="flex flex-wrap gap-2">
									<span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-orange-100 dark:bg-orange-900/30 text-orange-700 dark:text-orange-300 text-sm font-medium">
										<i className="ri-fire-line" />
										{recipe.calories} kcal
									</span>
									<span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-red-100 dark:bg-red-900/30 text-red-700 dark:text-red-300 text-sm font-medium">
										<i className="ri-heart-pulse-line" />
										{recipe.protein}g protein
									</span>
									<span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-amber-100 dark:bg-amber-900/30 text-amber-700 dark:text-amber-300 text-sm font-medium">
										<i className="ri-bread-line" />
										{recipe.carbs}g carbs
									</span>
									<span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-yellow-100 dark:bg-yellow-900/30 text-yellow-700 dark:text-yellow-300 text-sm font-medium">
										<i className="ri-drop-line" />
										{recipe.fat}g fat
									</span>
								</div>
							</div>
						</Link>
					))}
				</div>
			)}
		</div>
	);
}
