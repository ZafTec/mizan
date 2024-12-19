
export default function Page({ params }: { params: { ingredientId: string } }) {
	const { ingredientId } = params;

	return (
		<div>
			<h1 className="text-2xl font-bold">Ingredient {ingredientId}</h1>
		</div>
	)
}
