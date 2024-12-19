
export default async function Page({ params }: { params: Promise<{ ingredientId: string }> }) {
	const { ingredientId } = await params;

	return (
		<div>
			<h1 className="text-2xl font-bold">Ingredient {ingredientId}</h1>
		</div>
	)
}
