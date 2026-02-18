'use client';

import { useActionState, useEffect } from "react";
import { useRouter } from "next/navigation";
import { EMPTY_FORM_STATE, FormState } from "@/helper/FormErrorHandler";
import { FieldError } from "@/components/FieldError";
import { Ingredient } from "@/data/ingredient";
import Loading from "@/components/Loading";

interface IngredientFormProps {
    action: (prevState: FormState, formData: FormData) => Promise<FormState>;
    initialData?: Ingredient;
    submitText: string;
}

export default function IngredientForm({ action, initialData, submitText }: IngredientFormProps) {
    const [formState, formAction, isPending] = useActionState(action, EMPTY_FORM_STATE);
    const router = useRouter();

    useEffect(() => {
        if (formState.status === "success") {
            router.push("/admin/ingredients");
            router.refresh();
        }
    }, [formState.status, router]);

    return (
        <form action={formAction} className="space-y-6">
            {initialData?.id && <input type="hidden" name="id" value={initialData.id} />}

            <div className="card p-6 space-y-5">
                <h2 className="font-semibold text-slate-900 flex items-center gap-2 border-b border-slate-100 pb-4">
                    <i className="ri-information-line text-brand-500" />
                    General Information
                </h2>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <div className="md:col-span-2">
                        <label htmlFor="name" className="label">Ingredient Name</label>
                        <input
                            type="text"
                            id="name"
                            name="name"
                            defaultValue={initialData?.name}
                            className="input"
                            placeholder="e.g., Whole Wheat Bread"
                            required
                        />
                        <FieldError formState={formState} name="name" />
                    </div>
                </div>

                <div className="grid grid-cols-1 md:grid-cols-3 gap-4 pt-2">
                    <div>
                        <label htmlFor="servingSize" className="label">Serving Size</label>
                        <input
                            type="number"
                            id="servingSize"
                            name="servingSize"
                            defaultValue={initialData?.servingSize || 100}
                            className="input"
                            step="0.1"
                            required
                        />
                        <FieldError formState={formState} name="servingSize" />
                    </div>
                    <div>
                        <label htmlFor="servingUnit" className="label">Unit</label>
                        <input
                            type="text"
                            id="servingUnit"
                            name="servingUnit"
                            defaultValue={initialData?.servingUnit || "g"}
                            className="input"
                            required
                        />
                        <FieldError formState={formState} name="servingUnit" />
                    </div>
                    <div className="flex flex-col justify-end pb-3">
                        <label className="flex items-center gap-2 cursor-pointer select-none">
                            <input
                                type="checkbox"
                                name="isVerified"
                                value="true"
                                defaultChecked={initialData?.isVerified}
                                className="w-5 h-5 rounded border-slate-300 text-brand-600 focus:ring-brand-500"
                            />
                            <span className="text-sm font-medium text-slate-700">Verified Ingredient</span>
                        </label>
                    </div>
                </div>
            </div>

            <div className="card p-6 space-y-5">
                <h2 className="font-semibold text-slate-900 flex items-center gap-2 border-b border-slate-100 pb-4">
                    <i className="ri-heart-pulse-line text-brand-500" />
                    Nutrition (per 100g)
                </h2>

                <div className="grid grid-cols-2 md:grid-cols-5 gap-4">
                    <div>
                        <label htmlFor="calories" className="label font-bold text-orange-600">Calories</label>
                        <input
                            type="number"
                            id="calories"
                            name="calories"
                            defaultValue={initialData?.caloriesPer100g}
                            className="input border-orange-100 focus:border-orange-500 focus:ring-orange-200"
                            placeholder="0"
                            required
                        />
                    </div>
                    <div>
                        <label htmlFor="protein" className="label font-bold text-red-600">Protein (g)</label>
                        <input
                            type="number"
                            id="protein"
                            name="protein"
                            defaultValue={initialData?.proteinPer100g}
                            className="input border-red-100 focus:border-red-500 focus:ring-red-200"
                            step="0.1"
                            placeholder="0"
                            required
                        />
                    </div>
                    <div>
                        <label htmlFor="carbs" className="label font-bold text-amber-600">Carbs (g)</label>
                        <input
                            type="number"
                            id="carbs"
                            name="carbs"
                            defaultValue={initialData?.carbsPer100g}
                            className="input border-amber-100 focus:border-amber-500 focus:ring-amber-200"
                            step="0.1"
                            placeholder="0"
                            required
                        />
                    </div>
                    <div>
                        <label htmlFor="fat" className="label font-bold text-yellow-600">Fat (g)</label>
                        <input
                            type="number"
                            id="fat"
                            name="fat"
                            defaultValue={initialData?.fatPer100g}
                            className="input border-yellow-100 focus:border-yellow-500 focus:ring-yellow-200"
                            step="0.1"
                            placeholder="0"
                            required
                        />
                    </div>
                    <div>
                        <label htmlFor="fiber" className="label font-bold text-emerald-600">Fiber (g)</label>
                        <input
                            type="number"
                            id="fiber"
                            name="fiber"
                            defaultValue={initialData?.fiberPer100g}
                            className="input border-emerald-100 focus:border-emerald-500 focus:ring-emerald-200"
                            step="0.1"
                            placeholder="0"
                        />
                    </div>
                </div>
                <p className="text-xs text-slate-500 mt-2">
                    <i className="ri-information-line mr-1" />
                    Important: All nutrition values must be provided <strong>per 100g</strong> of the ingredient for consistent calculations.
                </p>
            </div>

            <div className="flex items-center gap-3">
                <button
                    type="button"
                    onClick={() => router.back()}
                    className="btn-secondary flex-1"
                >
                    Cancel
                </button>
                <button
                    type="submit"
                    disabled={isPending}
                    className="btn-primary flex-1 py-3"
                >
                    {isPending ? (
                        <>
                            <Loading size="sm" />
                            Saving...
                        </>
                    ) : (
                        submitText
                    )}
                </button>
            </div>

            {formState.status === "error" && (
                <div className="p-4 rounded-xl bg-red-50 text-red-600 text-sm font-medium border border-red-100">
                    <i className="ri-error-warning-line mr-2" />
                    {formState.message}
                </div>
            )}
        </form>
    );
}
