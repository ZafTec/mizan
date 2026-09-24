import { getIngredientById } from "@/data/ingredient";
import { getUserServer } from "@/helper/session";
import Link from "next/link";
import { redirect } from "next/navigation";
import EditIngredientForm from "./EditIngredientForm";

export default async function EditIngredientPage({ params }: { params: Promise<{ ingredientId: string }> }) {
	const { ingredientId } = await params;
	const [ingredient, user] = await Promise.all([
		getIngredientById(ingredientId),
		getUserServer(),
	]);

	if (user.role !== "admin") {
		redirect(`/ingredients/${ingredientId}`);
	}

	if (!ingredient) {
		return (
			<div className="min-h-[50vh] flex flex-col items-center justify-center">
				<div className="w-20 h-20 rounded-2xl bg-muted flex items-center justify-center mb-4">
					<i className="ri-leaf-line text-4xl text-muted-foreground" />
				</div>
				<h2 className="text-xl font-semibold text-foreground mb-2">Ingredient not found</h2>
				<p className="text-muted-foreground mb-6">The ingredient you&apos;re looking for doesn&apos;t exist.</p>
				<Link href="/ingredients" className="btn-primary">
					<i className="ri-arrow-left-line" />
					Back to Ingredients
				</Link>
			</div>
		);
	}

	return (
		<div className="max-w-3xl mx-auto space-y-6">
			<div className="flex items-center gap-4">
				<Link href={`/ingredients/${ingredientId}`} className="w-10 h-10 rounded-xl bg-muted hover:bg-accent flex items-center justify-center transition-colors">
					<i className="ri-arrow-left-line text-xl text-muted-foreground" />
				</Link>
				<div>
					<h1 className="text-3xl font-semibold tracking-tight text-foreground">Edit Ingredient</h1>
					<p className="text-muted-foreground capitalize">{ingredient.name}</p>
				</div>
			</div>

			<EditIngredientForm ingredient={ingredient} />
		</div>
	);
}
