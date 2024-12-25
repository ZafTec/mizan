
import TableHeaderCell from "@/components/IngredientTable/TableHeaderCell";
import { getAllIngredient } from "@/data/ingredient";
import getUser from "@/context/AuthProvider";
import Link from "next/link";
import SearchBar from "@/components/IngredientTable/SearchInputField";

export default async function Page(
	{ searchParams }: { searchParams: Promise<{ [key: string]: string | string[] | undefined }>}
) {
	const { } = await getUser();

	const { sortBy, searchIngredient } = await searchParams;

	// Fetch ingredient
	const ingredient = await getAllIngredient(searchIngredient as string, sortBy as string);

	return (
		<div className="flex flex-col py-4 px-4 md:px-16 gap-4">
			<div className="flex flex-row justify-between items-center gap-2">
				<div>
					{/* Search bar */}
					<SearchBar />
				</div>
				<div>
					{/* Add Button */}
					<Link href="/ingredients/add" className="bg-emerald-700 text-white px-2 py-2 rounded-lg">
						<i className="ri-add-large-line mr-2"></i>
						Add 
					</Link>
				</div>
			</div>
			<div className="overflow-x-auto">
				<table className="w-full border-b-2">
					<thead className="border-b-2 text-lg pb-2">
						<tr>
							<TableHeaderCell className="w-52">Name</TableHeaderCell>
							<TableHeaderCell>Calories</TableHeaderCell>
							<TableHeaderCell>Protein</TableHeaderCell>
							<TableHeaderCell>Fat</TableHeaderCell>
							<TableHeaderCell>Carbs</TableHeaderCell>
							<TableHeaderCell>Fiber</TableHeaderCell>
						</tr>
					</thead>
					<tbody className="divide-y-2">
						{/* Recipe rows */}
						{!ingredient || ingredient.length === 0 ? 
							<tr><td colSpan={6}>No ingredients found</td></tr> 
							: 
							ingredient.map((ingredient, index) => (
								<tr
									key={ingredient._id.toString()}
									className={index % 2 == 0 ? "bg-slate-200" : ""}
								>
									<td className="capitalize font-semibold">
										<Link href={`/ingredients/${ingredient._id}`} className="inline-block w-full">
											{ingredient.name}
										</Link>
									</td>
									<td className="text-center">{ingredient.macros.calories}</td>
									<td className="text-center">{ingredient.macros.protein}</td>
									<td className="text-center">{ingredient.macros.fat}</td>
									<td className="text-center">{ingredient.macros.carbs}</td>
									<td className="text-center">{ingredient.macros.fiber}</td>
								</tr>
							))
						}
					</tbody>
					<tfoot>
						<tr>
							<td colSpan={6}>
								{/* Pagination */}
							</td>
						</tr>
					</tfoot>
				</table>
			</div>
		</div>
	)
}
