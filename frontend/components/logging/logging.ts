export type FoodResult = {
  id: string;
  name: string;
  brand?: string | null;
  servingSize: number;
  servingUnit: string;
  caloriesPer100g: number;
  proteinPer100g: number;
  carbsPer100g: number;
  fatPer100g: number;
  lastUsedAt?: string | null;
};

export type RecipeResult = {
  id: string;
  title: string;
  isFavorited?: boolean;
  lastUsedAt?: string | null;
  nutrition?: {
    caloriesPerServing?: number | null;
    proteinGrams?: number | null;
    carbsGrams?: number | null;
    fatGrams?: number | null;
  };
};

export type PickerItem = {
  id: string;
  kind: "food" | "recipe";
  name: string;
  detail: string;
  servingSize: number;
  calories: number | null;
  protein: number | null;
  pinned?: boolean;
  lastUsedAt?: string | null;
};

export function foodItem(food: FoodResult): PickerItem {
  return {
    id: food.id,
    kind: "food",
    name: food.name,
    detail: food.brand || "Food",
    servingSize: food.servingSize > 0 ? food.servingSize : 100,
    calories: food.caloriesPer100g,
    protein: food.proteinPer100g,
    lastUsedAt: food.lastUsedAt,
  };
}

export function recipeItem(
  recipe: RecipeResult,
  pinned = recipe.isFavorited,
): PickerItem {
  return {
    id: recipe.id,
    kind: "recipe",
    name: recipe.title,
    detail: "Recipe",
    servingSize: 1,
    calories: recipe.nutrition?.caloriesPerServing ?? null,
    protein: recipe.nutrition?.proteinGrams ?? null,
    pinned,
    lastUsedAt: recipe.lastUsedAt,
  };
}

export type MealSelection = {
  key: string;
  item: PickerItem;
  amount: string;
};

export function nutritionForSelection(selection: MealSelection) {
  const multiplier =
    Number(selection.amount) / (selection.item.kind === "food" ? 100 : 1);
  return {
    calories:
      selection.item.calories === null
        ? null
        : selection.item.calories * multiplier,
    protein:
      selection.item.protein === null
        ? null
        : selection.item.protein * multiplier,
  };
}
