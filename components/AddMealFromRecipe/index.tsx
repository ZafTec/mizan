'use client';

import SubmitButton from "../AddIngredient/button";
import { FieldError } from "../FieldError";
import { useActionState, useEffect } from "react";
import { addMeal } from "@/data/meal";
import { EMPTY_FORM_STATE } from "@/helper/FormErrorHandler";
import Macros from "@/types/macro";

interface AddMealFromRecipeProps {
	name: string;
	macros: Macros;
}
export default function AddMealFromRecipe({ name, macros }: AddMealFromRecipeProps) {
	const [formState, action] = useActionState(addMeal, EMPTY_FORM_STATE);

	useEffect(() => {
		if (formState.status === "SUCCESS") {
			// Optionally reset the form or perform other actions on success
			// action.reset();
		}
	}, [formState.status]);

	return (
		<div className="flex flex-col py-4 md:px-10 lg:px-16 gap-4">
			<h1 className="text-4xl font-bold">Add Meal</h1>

			<form action={action} className="flex flex-col gap-4">
				<div className="flex flex-col md:flex-row gap-2">
					<div className="flex flex-1 flex-col gap-2">
						<label htmlFor="name">Name</label>
						<input
							type="text"
							id="name"
							name="name"
							className="border-2 border-gray-300 rounded-lg p-2"
							placeholder="Meal name"
							defaultValue={name}
						/>
						<FieldError formState={formState} name="name" />
					</div>
					<div className="flex flex-1 flex-col gap-2">
						<label htmlFor="mealType">Meal Type</label>
						<select
							id="mealType"
							name="mealType"
							className="border-2 border-gray-300 rounded-lg p-2"
						>
							<option value="Meal">Meal</option>
							<option value="Snack">Snack</option>
							<option value="Drink">Drink</option>
						</select>
						<FieldError formState={formState} name="mealType" />
					</div>
				</div>

				<div className="flex flex-col xl:flex-row gap-2">
					<div className="flex flex-col md:flex-row gap-2">
						<div className="flex flex-1 flex-col gap-2">
							<label htmlFor="calories">Calories [kCal]</label>
							<input
								type="number"
								id="calories"
								name="calories"
								min={0}
								defaultValue={macros.calories.toFixed()}
								className="border-2 border-gray-300 rounded-lg p-2"
							/>
							<FieldError formState={formState} name="calories" />
						</div>
						<div className="flex flex-1 flex-col gap-2">
							<label htmlFor="protein">Protein [grams]</label>
							<input
								type="number"
								id="protein"
								name="protein"
								min={0}
								defaultValue={macros.protein.toFixed()}
								className="border-2 border-gray-300 rounded-lg p-2"
							/>
							<FieldError formState={formState} name="protein" />
						</div>
						<div className="flex flex-1 flex-col gap-2">
							<label htmlFor="fat">Fat [grams]</label>
							<input
								type="number"
								id="fat"
								name="fat"
								min={0}
								defaultValue={macros.fat.toFixed()}
								className="border-2 border-gray-300 rounded-lg p-2"
							/>
							<FieldError formState={formState} name="fat" />
						</div>
					</div>
					<div className="flex flex-col md:flex-row gap-2">
						<div className="flex flex-1 flex-col gap-2">
							<label htmlFor="carbs">Carbs [grams]</label>
							<input
								type="number"
								id="carbs"
								name="carbs"
								min={0}
								defaultValue={macros.carbs.toFixed()}
								className="border-2 border-gray-300 rounded-lg p-2"
							/>
							<FieldError formState={formState} name="carbs" />
						</div>
						<div className="flex flex-1 flex-col gap-2">
							<label htmlFor="fiber">Fiber [grams]</label>
							<input
								type="number"
								id="fiber"
								name="fiber"
								min={0}
								defaultValue={macros.fiber.toFixed()}
								className="border-2 border-gray-300 rounded-lg p-2"
							/>
							<FieldError formState={formState} name="fiber" />
						</div>
					</div>
				</div>

				<div>
					<SubmitButton disabled={false} label="Add Meal" loading={<>Loading...</>} />
				</div>

				<div>
					{formState.message && <p>{formState.message}</p>}
				</div>
			</form>
		</div>
	);
}
