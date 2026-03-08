import { getIngredientById, updateIngredient } from "@/data/ingredient";
import IngredientForm from "../../IngredientForm";
import Link from "next/link";
import { notFound } from "next/navigation";

export default async function EditIngredientPage({
    params,
}: {
    params: Promise<{ id: string }>;
}) {
    const { id } = await params;
    const ingredient = await getIngredientById(id);

    if (!ingredient) {
        notFound();
    }

	    return (
	        <div className="max-w-2xl mx-auto space-y-6">
            <div className="flex items-center gap-4">
	                <Link href="/admin/ingredients" className="flex h-10 w-10 items-center justify-center rounded-xl border border-slate-100 bg-white transition-colors hover:bg-slate-50 dark:border-white/10 dark:bg-slate-950/75 dark:hover:bg-slate-900">
	                    <i className="ri-arrow-left-line text-slate-600 dark:text-slate-300" />
                </Link>
                <div>
	                    <h1 className="text-2xl font-bold text-slate-900 dark:text-slate-100">Edit Ingredient</h1>
	                    <p className="text-slate-500 dark:text-slate-400">Update details for &quot;{ingredient.name}&quot;</p>
                </div>
            </div>

            <IngredientForm
                action={updateIngredient}
                initialData={ingredient}
                submitText="Update Ingredient"
            />
        </div>
    );
}
