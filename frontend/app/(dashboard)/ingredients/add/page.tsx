"use client";

import { addIngredientData } from "@/data/ingredient";
import Link from "next/link";
import { useState } from "react";
import { useRouter } from "next/navigation";

export default function Page() {
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

		if (!name || isNaN(calories)) {
			setError("Name and calories are required");
			setIsPending(false);
			return;
		}

		try {
			await addIngredientData({
				name,
				brand,
				barcode,
				servingSize,
				calories,
				protein,
				carbs,
				fat,
				fiber,
			});

			setSuccess(true);
			// Invalidate Router Cache and navigate to ingredients page
			setTimeout(() => {
				router.refresh(); // Force fresh data from server first
				router.push("/ingredients"); // Then navigate
			}, 1500);
		} catch (err: any) {
			console.error("Failed to add ingredient:", err);
			setError(err.message || "Failed to add ingredient");
		} finally {
			setIsPending(false);
		}
	};

	return (
		<div className="max-w-3xl mx-auto space-y-6">
			{/* Header */}
			<div className="flex items-center gap-4">
				<Link href="/ingredients" className="w-10 h-10 rounded-xl bg-slate-100 hover:bg-slate-200 flex items-center justify-center transition-colors">
					<i className="ri-arrow-left-line text-xl text-slate-600" />
				</Link>
				<div>
					<h1 className="text-2xl font-bold text-slate-900">Add Ingredient</h1>
					<p className="text-slate-500">Add a new ingredient to the database</p>
				</div>
			</div>

			<form onSubmit={handleSubmit} className="space-y-6">
				{/* Basic Info Card */}
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
							placeholder="e.g., Chicken Breast, Brown Rice"
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
								placeholder="e.g., Organic Valley"
							/>
						</div>
						<div>
							<label htmlFor="barcode" className="label">Barcode (optional)</label>
							<input
								type="text"
								id="barcode"
								name="barcode"
								className="input"
								placeholder="e.g., 123456789"
							/>
						</div>
					</div>
				</div>

				{/* Nutrition Card */}
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
								defaultValue={0}
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
								defaultValue={0}
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
								defaultValue={0}
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
								defaultValue={0}
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
								defaultValue={0}
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
								defaultValue={100}
								className="input"
							/>
						</div>
					</div>
				</div>

				{/* Status Messages */}
				{success && (
					<div className="flex items-center gap-2 p-4 rounded-xl bg-green-50 text-green-600">
						<i className="ri-checkbox-circle-line text-xl" />
						<span>Ingredient added successfully! Redirecting...</span>
					</div>
				)}

				{error && (
					<div className="flex items-center gap-2 p-4 rounded-xl bg-red-50 text-red-600">
						<i className="ri-error-warning-line text-xl" />
						<span>{error}</span>
					</div>
				)}

				{/* Submit Button */}
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
							Adding Ingredient...
						</>
					) : (
						<>
							<i className="ri-check-line text-xl" />
							Add Ingredient
						</>
					)}
				</button>
			</form>
		</div>
	);
}
