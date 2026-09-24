import { getIngredientById } from "@/data/ingredient";
import { getUserOptionalServer } from "@/helper/session";
import Link from "next/link";
import DeleteIngredientButton from "./DeleteIngredientButton";

export default async function Page({ params }: { params: Promise<{ ingredientId: string }> }) {
	const { ingredientId } = await params;
	const [ingredient, user] = await Promise.all([
		getIngredientById(ingredientId),
		getUserOptionalServer(),
	]);
	const isAdmin = user?.role === "admin";

	if (!ingredient) {
		return (
			<div className="min-h-[50vh] flex flex-col items-center justify-center">
				<div className="w-20 h-20 rounded-2xl bg-muted flex items-center justify-center mb-4">
					<i className="ri-leaf-line text-4xl text-muted-foreground" aria-hidden="true" />
				</div>
				<h2 className="text-xl font-semibold text-foreground mb-2">Ingredient not found</h2>
				<p className="text-muted-foreground mb-6">The ingredient you&apos;re looking for doesn&apos;t exist.</p>
				<Link href="/ingredients" className="btn-primary">
					<i className="ri-arrow-left-line" aria-hidden="true" />
					Back to Ingredients
				</Link>
			</div>
		);
	}

	const totalMacros = ingredient.proteinPer100g + ingredient.carbsPer100g + ingredient.fatPer100g;
	const proteinPercentage = totalMacros > 0 ? (ingredient.proteinPer100g / totalMacros) * 100 : 0;
	const carbsPercentage = totalMacros > 0 ? (ingredient.carbsPer100g / totalMacros) * 100 : 0;
	const fatPercentage = totalMacros > 0 ? (ingredient.fatPer100g / totalMacros) * 100 : 0;
	const proteinCalRatio = ingredient.caloriesPer100g > 0
		? (ingredient.proteinPer100g * 4 / ingredient.caloriesPer100g) * 100
		: 0;

	// Same macro colours as the Today page; the chart tokens switch with the theme.
	const nutrients = [
		{ label: "Calories", value: `${ingredient.caloriesPer100g}`, icon: "ri-fire-line", color: "var(--foreground)" },
		{ label: "Protein", value: `${ingredient.proteinPer100g}g`, icon: "ri-heart-pulse-line", color: "var(--chart-1)" },
		{ label: "Carbs", value: `${ingredient.carbsPer100g}g`, icon: "ri-bread-line", color: "var(--chart-2)" },
		{ label: "Fat", value: `${ingredient.fatPer100g}g`, icon: "ri-drop-line", color: "var(--chart-3)" },
		{ label: "Fiber", value: `${ingredient.fiberPer100g ?? 0}g`, icon: "ri-leaf-line", color: "var(--muted-foreground)" },
	];
	const macros = [
		{ label: "Protein", percentage: proteinPercentage, color: "var(--chart-1)" },
		{ label: "Carbs", percentage: carbsPercentage, color: "var(--chart-2)" },
		{ label: "Fat", percentage: fatPercentage, color: "var(--chart-3)" },
	];

	return (
		<div className="max-w-3xl mx-auto space-y-6">
			<div className="flex items-center gap-4">
				<Link
					href="/ingredients"
					aria-label="Back to Ingredients"
					className="w-10 h-10 shrink-0 rounded-xl border border-border bg-card text-foreground hover:bg-muted flex items-center justify-center transition-colors"
				>
					<i className="ri-arrow-left-line text-xl" aria-hidden="true" />
				</Link>
				<div className="flex-1 min-w-0">
					<div className="flex flex-wrap items-center gap-3">
						<h1 className="text-3xl font-semibold tracking-tight text-foreground capitalize break-words">{ingredient.name}</h1>
						{ingredient.isVerified && (
							<span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-2xl border border-border bg-muted text-foreground text-xs font-medium">
								<i className="ri-verified-badge-line" style={{ color: "var(--chart-1)" }} aria-hidden="true" />
								Verified
							</span>
						)}
					</div>
					<p className="text-muted-foreground">Per {ingredient.servingSize} {ingredient.servingUnit}</p>
				</div>
			</div>

			<div className="card p-6">
				<h2 className="font-semibold text-card-foreground mb-4">Nutritional Information</h2>
				<div className="grid grid-cols-2 sm:grid-cols-5 gap-3">
					{nutrients.map((nutrient) => (
						<div key={nutrient.label} className="text-center p-4 rounded-xl border border-border bg-background">
							<i className={`${nutrient.icon} text-xl`} style={{ color: nutrient.color }} aria-hidden="true" />
							<p className="mt-1 text-3xl font-semibold tracking-tight text-foreground tabular-nums">{nutrient.value}</p>
							<p className="text-xs text-muted-foreground">{nutrient.label}</p>
						</div>
					))}
				</div>
				{proteinCalRatio > 0 && (
					<div className="mt-4 flex items-center gap-3 p-3 rounded-xl border border-border bg-background">
						<i className="ri-percent-line text-xl text-muted-foreground" aria-hidden="true" />
						<div>
							<p className="text-lg font-bold text-foreground tabular-nums">{proteinCalRatio.toFixed(0)}%</p>
							<p className="text-xs text-muted-foreground">Protein-to-Calorie Ratio</p>
						</div>
					</div>
				)}
			</div>

			<div className="card p-6">
				<h2 className="font-semibold text-card-foreground flex items-center gap-2 mb-4">
					<i className="ri-pie-chart-2-line text-muted-foreground" aria-hidden="true" />
					Macronutrient Distribution
				</h2>
				<div
					role="img"
					aria-label={macros.map((macro) => `${macro.label} ${macro.percentage.toFixed(0)}%`).join(", ")}
					className="h-4 flex rounded-full overflow-hidden mb-4 bg-muted"
				>
					{macros.map((macro) => (
						<div
							key={macro.label}
							style={{ width: `${macro.percentage}%`, background: macro.color }}
							className="transition-all"
							title={`${macro.label}: ${macro.percentage.toFixed(1)}%`}
						/>
					))}
				</div>
				<div className="flex flex-wrap gap-6">
					{macros.map((macro) => (
						<div key={macro.label} className="flex items-center gap-2">
							<div className="w-4 h-4 rounded-md" style={{ background: macro.color }} aria-hidden="true" />
							<span className="text-sm text-muted-foreground">{macro.label}</span>
							<span className="text-sm font-semibold text-foreground tabular-nums">{macro.percentage.toFixed(0)}%</span>
						</div>
					))}
				</div>
			</div>

			{isAdmin && (
				<div className="card p-6">
					<div className="flex items-center justify-between">
						<div className="text-sm text-muted-foreground">
							{ingredient.brand && <span>Brand: {ingredient.brand}</span>}
						</div>
						<div className="flex gap-2">
							<Link href={`/ingredients/${ingredientId}/edit`} className="btn-secondary text-sm px-3 py-1.5">
								<i className="ri-edit-line" aria-hidden="true" />
								Edit
							</Link>
							<DeleteIngredientButton id={ingredientId} />
						</div>
					</div>
				</div>
			)}
		</div>
	);
}
