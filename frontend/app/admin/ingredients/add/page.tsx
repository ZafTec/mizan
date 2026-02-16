import { addIngredient } from "@/data/ingredient";
import IngredientForm from "../IngredientForm";
import Link from "next/link";

export default function AddIngredientPage() {
    return (
        <div className="max-w-2xl mx-auto space-y-6">
            <div className="flex items-center gap-4">
                <Link href="/admin/ingredients" className="w-10 h-10 rounded-xl bg-white dark:bg-slate-900 border border-slate-100 dark:border-slate-700 flex items-center justify-center hover:bg-slate-50 dark:hover:bg-slate-800 transition-colors">
                    <i className="ri-arrow-left-line text-slate-600 dark:text-slate-400" />
                </Link>
                <div>
                    <h1 className="text-2xl font-bold text-slate-900 dark:text-slate-100">Add New Ingredient</h1>
                    <p className="text-slate-500 dark:text-slate-400">Create a new building block for recipes</p>
                </div>
            </div>

            <IngredientForm
                action={addIngredient}
                submitText="Create Ingredient"
            />
        </div>
    );
}
