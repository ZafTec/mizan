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
            <header className="flex items-center gap-4">
                <Link href="/admin/ingredients" className="flex h-10 w-10 items-center justify-center rounded-xl border border-charcoal-blue-200 bg-white transition-colors hover:bg-charcoal-blue-50 dark:border-white/10 dark:bg-charcoal-blue-950 dark:hover:bg-charcoal-blue-900">
                    <i className="ri-arrow-left-line text-charcoal-blue-600 dark:text-charcoal-blue-300" />
                </Link>
                <div className="space-y-2">
                    <p className="eyebrow">Catalogue</p>
                    <h1 className="text-3xl font-semibold tracking-tight text-charcoal-blue-900 dark:text-charcoal-blue-50 sm:text-4xl">
                        Edit ingredient
                    </h1>
                    <p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
                        Update details for &quot;{ingredient.name}&quot;.
                    </p>
                </div>
            </header>

            <IngredientForm
                action={updateIngredient}
                initialData={ingredient}
                submitText="Update Ingredient"
            />
        </div>
    );
}
