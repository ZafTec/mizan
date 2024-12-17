
import TableHeaderCell from "@/components/IngredientTable/TableHeaderCell";
import { getAllIngredient } from "@/data/ingredient";
import getUser from "@/context/AuthProvider";
import Link from "next/link";

export default async function Page() {
	const { user } = await getUser();
	// Fetch ingredient
	const ingredient = await getAllIngredient();
	console.log(ingredient);

	return (
		<div className="flex flex-col py-4 px-16 gap-4">
			<div className="flex flex-row justify-between">
				<div>
					{/* Search bar */}
					<input type="text" placeholder="Search ingredient" className="border-2 border-gray-300 rounded-lg p-2" />
				</div>
				<div>
					{/* Add Button */}
					<Link href="/ingredients/add" className="bg-blue-500 text-white px-4 py-2 rounded-lg">
						<i className="ri-add-large-line"></i>
						Add 
					</Link>
				</div>
			</div>
			<div className="">
				<table className="w-full border-b-2">
					<thead className="border-b-2">
						<tr>
							<TableHeaderCell>Name</TableHeaderCell>
							<TableHeaderCell>Calories</TableHeaderCell>
							<TableHeaderCell>Protein</TableHeaderCell>
							<TableHeaderCell>Fat</TableHeaderCell>
							<TableHeaderCell>Carbs</TableHeaderCell>
							<TableHeaderCell>Fiber</TableHeaderCell>
						</tr>
					</thead>
					<tbody>
						{/* Recipe rows */}
						{!ingredient || ingredient.length === 0 ? 
							<tr><td colSpan={6}>No ingredients found</td></tr> 
							: 
							ingredient.map((ingredient) => (
								<tr key={ingredient.id.toString()}>
									<td>{ingredient.name}</td>
									<td>{ingredient.macros.calories}</td>
									<td>{ingredient.macros.protein}</td>
									<td>{ingredient.macros.fat}</td>
									<td>{ingredient.macros.carbs}</td>
									<td>{ingredient.macros.fiber}</td>
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
