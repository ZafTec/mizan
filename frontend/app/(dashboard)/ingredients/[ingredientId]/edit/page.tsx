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
				<div className="w-20 h-20 rounded-2xl bg-slate-100 flex items-center justify-center mb-4">
					<i className="ri-leaf-line text-4xl text-slate-400" />
				</div>
				<h2 className="text-xl font-semibold text-slate-900 mb-2">Ingredient not found</h2>
				<p className="text-slate-500 mb-6">The ingredient you&apos;re looking for doesn&apos;t exist.</p>
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
				<Link href={`/ingredients/${ingredientId}`} className="w-10 h-10 rounded-xl bg-slate-100 hover:bg-slate-200 flex items-center justify-center transition-colors">
					<i className="ri-arrow-left-line text-xl text-slate-600" />
				</Link>
				<div>
					<h1 className="text-2xl font-bold text-slate-900">Edit Ingredient</h1>
					<p className="text-slate-500 capitalize">{ingredient.name}</p>
				</div>
			</div>

			<EditIngredientForm ingredient={ingredient} />
		</div>
	);
}
