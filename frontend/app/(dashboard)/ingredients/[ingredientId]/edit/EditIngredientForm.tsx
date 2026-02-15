"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { updateIngredientData, type Ingredient } from "@/data/ingredient";

export default function EditIngredientForm({ ingredient }: { ingredient: Ingredient }) {
	const router = useRouter();
	const [isPending, setIsPending] = useState(false);
	const [error, setError] = useState<string | null>(null);
	const [success, setSuccess] = useState(false);

	const handleSubmit = async (e: React.FormEvent<HTMLFormElement>) => {
		e.preventDefault();
		setIsPending(true);
		setError(null);
		setSuccess(false);

		const formData = new FormData(e.currentTarget);
		const name = formData.get("name") as string;
		const brand = formData.get("brand") as string;
		const barcode = formData.get("barcode") as string;
		const servingSize = parseFloat(formData.get("servingSize") as string) || 100;
		const calories = parseInt(formData.get("calories") as string) || 0;
		const protein = parseFloat(formData.get("protein") as string) || 0;
		const carbs = parseFloat(formData.get("carbs") as string) || 0;
		const fat = parseFloat(formData.get("fat") as string) || 0;
		const fiber = parseFloat(formData.get("fiber") as string) || 0;
		const isVerified = (formData.get("isVerified") as string) === "on";

		if (!name || isNaN(calories)) {
			setError("Name and calories are required");
			setIsPending(false);
			return;
		}

		try {
			await updateIngredientData(ingredient.id, {
				name,
				brand,
				barcode,
				servingSize,
				calories,
				protein,
				carbs,
				fat,
				fiber,
				isVerified,
			});

			setSuccess(true);
			setTimeout(() => {
				router.refresh();
				router.push(`/ingredients/${ingredient.id}`);
			}, 1000);
		} catch (err: any) {
			setError(err.message || "Failed to update ingredient");
		} finally {
			setIsPending(false);
		}
	};

	return (
		<form onSubmit={handleSubmit} className="space-y-6">
			<div className="card p-6 space-y-5">
				<h2 className="font-semibold text-slate-900 flex items-center gap-2">
					<i className="ri-leaf-line text-brand-500" />
					Basic Information
				</h2>
				<div>
					<label htmlFor="name" className="label">Ingredient Name</label>
					<input
						type="text"
						id="name"
						name="name"
						className="input"
						defaultValue={ingredient.name}
						required
					/>
				</div>
				<div className="grid grid-cols-2 gap-4">
					<div>
						<label htmlFor="brand" className="label">Brand (optional)</label>
						<input
							type="text"
							id="brand"
							name="brand"
							className="input"
							defaultValue={ingredient.brand || ""}
						/>
					</div>
					<div>
						<label htmlFor="barcode" className="label">Barcode (optional)</label>
						<input
							type="text"
							id="barcode"
							name="barcode"
							className="input"
							defaultValue={ingredient.barcode || ""}
						/>
					</div>
				</div>
			</div>

			<div className="card p-6 space-y-5">
				<h2 className="font-semibold text-slate-900 flex items-center gap-2">
					<i className="ri-heart-pulse-line text-brand-500" />
					Nutritional Information
				</h2>

				<div className="grid grid-cols-2 sm:grid-cols-3 gap-4">
					<div>
						<label htmlFor="calories" className="label">
							<span className="flex items-center gap-1.5">
								<span className="w-2 h-2 rounded-full bg-orange-500" />
								Calories (kcal)
							</span>
						</label>
						<input
							type="number"
							id="calories"
							name="calories"
							min={0}
							defaultValue={ingredient.caloriesPer100g}
							className="input"
						/>
					</div>
					<div>
						<label htmlFor="protein" className="label">
							<span className="flex items-center gap-1.5">
								<span className="w-2 h-2 rounded-full bg-red-500" />
								Protein (g)
							</span>
						</label>
						<input
							type="number"
							id="protein"
							name="protein"
							min={0}
							step="0.1"
							defaultValue={ingredient.proteinPer100g}
							className="input"
						/>
					</div>
					<div>
						<label htmlFor="carbs" className="label">
							<span className="flex items-center gap-1.5">
								<span className="w-2 h-2 rounded-full bg-amber-500" />
								Carbs (g)
							</span>
						</label>
						<input
							type="number"
							id="carbs"
							name="carbs"
							min={0}
							step="0.1"
							defaultValue={ingredient.carbsPer100g}
							className="input"
						/>
					</div>
					<div>
						<label htmlFor="fat" className="label">
							<span className="flex items-center gap-1.5">
								<span className="w-2 h-2 rounded-full bg-yellow-500" />
								Fat (g)
							</span>
						</label>
						<input
							type="number"
							id="fat"
							name="fat"
							min={0}
							step="0.1"
							defaultValue={ingredient.fatPer100g}
							className="input"
						/>
					</div>
					<div>
						<label htmlFor="fiber" className="label">
							<span className="flex items-center gap-1.5">
								<span className="w-2 h-2 rounded-full bg-green-500" />
								Fiber (g)
							</span>
						</label>
						<input
							type="number"
							id="fiber"
							name="fiber"
							min={0}
							step="0.1"
							defaultValue={ingredient.fiberPer100g ?? 0}
							className="input"
						/>
					</div>
					<div>
						<label htmlFor="servingSize" className="label">
							<span className="flex items-center gap-1.5">
								<span className="w-2 h-2 rounded-full bg-slate-400" />
								Serving Size (g)
							</span>
						</label>
						<input
							type="number"
							id="servingSize"
							name="servingSize"
							min={1}
							defaultValue={ingredient.servingSize}
							className="input"
						/>
					</div>
				</div>
			</div>

			<div className="card p-6">
				<label className="flex items-center gap-3 cursor-pointer">
					<input
						type="checkbox"
						name="isVerified"
						defaultChecked={ingredient.isVerified}
						className="w-4 h-4 rounded border-slate-300 text-brand-600 focus:ring-brand-500"
					/>
					<span className="text-sm font-medium text-slate-700">Mark as verified</span>
				</label>
			</div>

			{success && (
				<div className="flex items-center gap-2 p-4 rounded-xl bg-green-50 text-green-600">
					<i className="ri-checkbox-circle-line text-xl" />
					<span>Ingredient updated successfully! Redirecting...</span>
				</div>
			)}

			{error && (
				<div className="flex items-center gap-2 p-4 rounded-xl bg-red-50 text-red-600">
					<i className="ri-error-warning-line text-xl" />
					<span>{error}</span>
				</div>
			)}

			<button
				type="submit"
				disabled={isPending}
				className="btn-primary w-full py-3"
			>
				{isPending ? (
					<>
						<svg className="animate-spin h-5 w-5" fill="none" viewBox="0 0 24 24">
							<circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
							<path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
						</svg>
						Updating...
					</>
				) : (
					<>
						<i className="ri-check-line text-xl" />
						Update Ingredient
					</>
				)}
			</button>
		</form>
	);
}
