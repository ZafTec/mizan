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
                <Link href="/admin/ingredients" className="w-10 h-10 rounded-xl bg-white border border-slate-100 flex items-center justify-center hover:bg-slate-50 transition-colors">
                    <i className="ri-arrow-left-line text-slate-600" />
                </Link>
                <div>
                    <h1 className="text-2xl font-bold text-slate-900">Edit Ingredient</h1>
                    <p className="text-slate-500">Update details for &quot;{ingredient.name}&quot;</p>
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
