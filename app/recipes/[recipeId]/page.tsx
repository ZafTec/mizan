export default function Page({ params }: { params: { recipeId: string } }) {
    const { recipeId } = params;
    return (
        <div>
            <h1 className="text-2xl font-bold">Recipe {recipeId}</h1>
            {/* Statistic page */}
            
        </div>
    )
}
