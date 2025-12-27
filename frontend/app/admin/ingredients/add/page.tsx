import { addIngredient } from "@/data/ingredient";
import IngredientForm from "../IngredientForm";
import Link from "next/link";

export default function AddIngredientPage() {
    return (
        <div className="max-w-2xl mx-auto space-y-6">
            <div className="flex items-center gap-4">
                <Link href="/admin/ingredients" className="w-10 h-10 rounded-xl bg-white border border-slate-100 flex items-center justify-center hover:bg-slate-50 transition-colors">
                    <i className="ri-arrow-left-line text-slate-600" />
                </Link>
                <div>
                    <h1 className="text-2xl font-bold text-slate-900">Add New Ingredient</h1>
                    <p className="text-slate-500">Create a new building block for recipes</p>
                </div>
            </div>

            <IngredientForm
                action={addIngredient}
                submitText="Create Ingredient"
            />
        </div>
    );
}
