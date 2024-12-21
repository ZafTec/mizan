'use client';


export default function RecipeMoreButton({ recipeId }: { recipeId: string }) {

	function handleClicked(event: React.MouseEvent<HTMLButtonElement>) {
		event.preventDefault();
		event.stopPropagation();

		console.log(`More button clicked for recipe ${recipeId}`);
	}

	return (
		<button
			className="text-gray-400 hover:text-gray-600 z-10 hover:bg-emerald-50 w-8 h-8 rounded-full"
			onClick={handleClicked}
			type="button"
		>
			{/* Three dots menu icon */}
			<i className="ri-more-2-line ri-xl font-bold"></i>
		</button>
	)
}
