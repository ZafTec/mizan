
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
			<div className="flex items-center gap-4">
				<Link href={`/recipes/${recipeId}`} className="w-10 h-10 rounded-xl bg-slate-100 hover:bg-slate-200 flex items-center justify-center transition-colors">
					<i className="ri-arrow-left-line text-xl text-slate-600" />
				</Link>
				<div>
					<h1 className="text-2xl font-bold text-slate-900">Edit Recipe</h1>
					<p className="text-slate-500">Update your recipe details and nutrition</p>
				</div>
			</div>

			<EditRecipeForm recipe={recipe} />
		</div>
	)
}
