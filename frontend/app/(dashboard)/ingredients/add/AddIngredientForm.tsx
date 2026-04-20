"use client";

import { addIngredientData } from "@/data/ingredient";
import Loading from "@/components/Loading";
import Link from "next/link";
import { useState } from "react";
import { useRouter } from "next/navigation";

export default function AddIngredientForm() {
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
			setTimeout(() => {
				router.refresh();
				router.push("/ingredients");
			}, 1500);
		} catch (err: any) {
			console.error("Failed to add ingredient:", err);
			setError(err.message || "Failed to add ingredient");
		} finally {
			setIsPending(false);
		}
	};

	return (
		<div className="mx-auto max-w-3xl space-y-6">
			<div className="flex items-center gap-4">
				<Link href="/ingredients" className="flex h-10 w-10 items-center justify-center rounded-xl border border-slate-200 bg-white text-slate-600 transition-colors hover:bg-slate-100 hover:text-slate-900 dark:border-white/10 dark:bg-slate-950/75 dark:text-slate-300 dark:hover:bg-slate-900 dark:hover:text-slate-100">
					<i className="ri-arrow-left-line text-xl" />
				</Link>
				<div>
					<h1 className="text-2xl font-bold text-charcoal-blue-900 dark:text-charcoal-blue-100">Add Ingredient</h1>
					<p className="text-slate-600 dark:text-slate-400">Add a new ingredient to the database</p>
				</div>
			</div>

			<form onSubmit={handleSubmit} className="space-y-6">
				<div className="card p-6 space-y-5">
					<h2 className="flex items-center gap-2 font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100">
						<i className="ri-leaf-line text-brand-500 dark:text-brand-400" />
						Basic Information
					</h2>
					<div>
						<label htmlFor="name" className="label">Ingredient Name</label>
						<input type="text" id="name" name="name" className="input" placeholder="e.g., Chicken Breast, Brown Rice" required />
					</div>
					<div className="grid grid-cols-2 gap-4">
						<div>
							<label htmlFor="brand" className="label">Brand (optional)</label>
							<input type="text" id="brand" name="brand" className="input" placeholder="e.g., Organic Valley" />
						</div>
						<div>
							<label htmlFor="barcode" className="label">Barcode (optional)</label>
							<input type="text" id="barcode" name="barcode" className="input" placeholder="e.g., 123456789" />
						</div>
					</div>
				</div>

				<div className="card p-6 space-y-5">
					<h2 className="flex items-center gap-2 font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-100">
						<i className="ri-heart-pulse-line text-brand-500 dark:text-brand-400" />
						Nutritional Information
					</h2>

					<div className="grid grid-cols-2 gap-4 sm:grid-cols-3">
						<div>
							<label htmlFor="calories" className="label">
								<span className="flex items-center gap-1.5">
									<span className="h-2 w-2 rounded-sm bg-orange-500" />
									Calories (kcal)
								</span>
							</label>
							<input type="number" id="calories" name="calories" min={0} defaultValue={0} className="input" />
						</div>
						<div>
							<label htmlFor="protein" className="label">
								<span className="flex items-center gap-1.5">
									<span className="h-2 w-2 rounded-sm bg-red-500" />
									Protein (g)
								</span>
							</label>
							<input type="number" id="protein" name="protein" min={0} step="0.1" defaultValue={0} className="input" />
						</div>
						<div>
							<label htmlFor="carbs" className="label">
								<span className="flex items-center gap-1.5">
									<span className="h-2 w-2 rounded-sm bg-amber-500" />
									Carbs (g)
								</span>
							</label>
							<input type="number" id="carbs" name="carbs" min={0} step="0.1" defaultValue={0} className="input" />
						</div>
						<div>
							<label htmlFor="fat" className="label">
								<span className="flex items-center gap-1.5">
									<span className="h-2 w-2 rounded-sm bg-yellow-500" />
									Fat (g)
								</span>
							</label>
							<input type="number" id="fat" name="fat" min={0} step="0.1" defaultValue={0} className="input" />
						</div>
						<div>
							<label htmlFor="fiber" className="label">
								<span className="flex items-center gap-1.5">
									<span className="h-2 w-2 rounded-sm bg-green-500" />
									Fiber (g)
								</span>
							</label>
							<input type="number" id="fiber" name="fiber" min={0} step="0.1" defaultValue={0} className="input" />
						</div>
						<div>
							<label htmlFor="servingSize" className="label">
								<span className="flex items-center gap-1.5">
									<span className="h-2 w-2 rounded-sm bg-slate-400 dark:bg-slate-500" />
									Serving Size (g)
								</span>
							</label>
							<input type="number" id="servingSize" name="servingSize" min={1} defaultValue={100} className="input" />
						</div>
					</div>
				</div>

				{success ? (
					<div className="flex items-center gap-2 rounded-xl bg-green-50 p-4 text-green-700 dark:bg-green-500/10 dark:text-green-300">
						<i className="ri-checkbox-circle-line text-xl" />
						<span>Ingredient added successfully! Redirecting...</span>
					</div>
				) : null}

				{error ? (
					<div className="flex items-center gap-2 rounded-xl bg-red-50 p-4 text-red-700 dark:bg-red-500/10 dark:text-red-300">
						<i className="ri-error-warning-line text-xl" />
						<span>{error}</span>
					</div>
				) : null}

				<button type="submit" disabled={isPending} className="btn-primary w-full py-3">
					{isPending ? (
						<>
							<Loading size="sm" />
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
