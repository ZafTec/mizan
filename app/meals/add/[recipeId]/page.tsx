
import AddMealFromRecipe from "@/components/AddMealFromRecipe";
import { getRecipeById } from "@/data/recipe";


export default async function Page({ params }: { params: Promise<{ recipeId: string }> }) {
	const { recipeId } = await params;
    const recipe = await getRecipeById(recipeId);

    if (!recipe) {
        return <div>Recipe not found</div>;
    }
	return (
		<div>
			<h1 className="text-2xl font-bold text-red-500">Add meal from Recipe</h1>
			<AddMealFromRecipe name={recipe.name} macros={recipe.totalMacros}  />
		</div>
	)
}
