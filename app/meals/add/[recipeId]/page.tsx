

export default async function Page({ params }: { params: Promise<{ recipeId: string }> }) {
	const { recipeId } = await params;
	return (
		<div>
			<h1 className="text-2xl font-bold text-red-500">Add meal with id {recipeId}</h1>
			
		</div>
	)
}
