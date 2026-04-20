
import { getRecipeById } from "@/data/recipe";
import EditRecipeForm from "@/components/Recipes/EditRecipeForm";
import Link from "next/link";
import { notFound } from "next/navigation";

export default async function Page({ params }: { params: Promise<{ recipeId: string }> }) {
	const { recipeId } = await params;
	const recipe = await getRecipeById(recipeId);

	if (!recipe) {
		notFound();
	}

	return (
		<div className="max-w-4xl mx-auto space-y-6">
			{/* Header */}
			<header className="flex items-center gap-4">
				<Link href={`/recipes/${recipeId}`} className="flex h-10 w-10 items-center justify-center rounded-xl bg-charcoal-blue-100 transition-colors hover:bg-charcoal-blue-200 dark:bg-charcoal-blue-900/60 dark:hover:bg-charcoal-blue-900">
					<i className="ri-arrow-left-line text-xl text-charcoal-blue-600 dark:text-charcoal-blue-300" />
				</Link>
				<div className="space-y-2">
					<p className="eyebrow">Cookbook</p>
					<h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
						Edit recipe
					</h1>
					<p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
						Update your recipe details and nutrition.
					</p>
				</div>
			</header>

			<EditRecipeForm recipe={recipe} />
		</div>
	)
}
