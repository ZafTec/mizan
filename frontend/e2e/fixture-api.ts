import type { BodyMeasurement } from "../data/bodyMeasurement";
import type { Ingredient } from "../data/ingredient";
import type { MealEntry } from "../data/meal";
import type { WorkoutSummary } from "../data/workout";

const port = Number(process.env.PLAYWRIGHT_API_PORT ?? 5100);
const webOrigin = process.env.PLAYWRIGHT_BASE_URL ?? "http://localhost:3100";
const today = new Date().toISOString().slice(0, 10);
const dateAgo = (days: number) => {
	const date = new Date(`${today}T12:00:00Z`);
	date.setUTCDate(date.getUTCDate() - days);
	return date.toISOString().slice(0, 10);
};
const food: Ingredient = {
	id: "11111111-1111-4111-8111-111111111111", name: "Greek yogurt", servingSize: 100,
	servingUnit: "g", caloriesPer100g: 97, proteinPer100g: 9, carbsPer100g: 4,
	fatPer100g: 5, fiberPer100g: 0, proteinCalorieRatio: 37.1, isVerified: true,
};
const oats: Ingredient = {
	...food, id: "22222222-2222-4222-8222-222222222222", name: "Rolled oats",
	caloriesPer100g: 389, proteinPer100g: 17, carbsPer100g: 66, fatPer100g: 7, fiberPer100g: 11,
};
const foods = [food, oats];
const user = {
	id: "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa", email: "alex@example.test", name: "Alex Morgan",
	role: "user", emailVerified: true, themePreference: "light", compactMode: false,
	reduceAnimations: false, hasPassword: true, timeZoneId: "UTC", image: null,
};
type FixtureState = {
	userId: string;
	meals: MealEntry[];
	measurements: BodyMeasurement[];
	workouts: WorkoutSummary[];
	recipes: Record<string, unknown>[];
	draft: { payload: string; updatedAt: string } | null;
	empty: boolean;
	unread: number;
	failures: string[];
	writeFailures: Record<string, number>;
	unhandled: string[];
	writes: { path: string; body: Record<string, unknown> }[];
};
const states = new Map<string, FixtureState>();
function initialState(empty = false): FixtureState {
	return {
		userId: user.id, empty, unread: 0, draft: null, failures: [], writeFailures: {}, unhandled: [], writes: [], recipes: empty ? [] : [{
			id: "33333333-3333-4333-8333-333333333333", title: "Yogurt and oats", servings: 1, isFavorited: true, isOwner: true,
			isPublic: true, description: "A breakfast to come back to.", instructions: "Stir the yogurt and oats together. Serve chilled.",
			ingredients: [{ foodId: food.id, foodName: food.name, ingredientText: food.name, amount: 200, unit: "g" }, { foodId: oats.id, foodName: oats.name, ingredientText: oats.name, amount: 50, unit: "g" }],
			lastUsedAt: `${dateAgo(1)}T08:00:00Z`, nutrition: { caloriesPerServing: 389, proteinGrams: 26.5, carbsGrams: 41, fatGrams: 13.5, fiberGrams: 5.5 },
		}],
		meals: empty ? [] : [
			{ id: "meal-yogurt", foodId: food.id, name: "Greek yogurt", mealType: "BREAKFAST", servings: 2, calories: 194, proteinGrams: 18, carbsGrams: 8, fatGrams: 10, fiberGrams: 0, loggedAt: `${today}T08:15:00Z` },
			{ id: "meal-oats", foodId: oats.id, name: "Rolled oats", mealType: "BREAKFAST", servings: 0.5, calories: 195, proteinGrams: 8.5, carbsGrams: 33, fatGrams: 3.5, fiberGrams: 5.5, loggedAt: `${today}T08:15:00Z` },
			{ id: "meal-yesterday", name: "Chicken and rice", mealType: "LUNCH", servings: 1, calories: 620, proteinGrams: 42, carbsGrams: 65, fatGrams: 19, fiberGrams: 4, loggedAt: `${dateAgo(1)}T12:30:00Z` },
		],
		measurements: empty ? [] : [0, 4, 8, 12, 16, 20, 24, 28].map((days, index) => ({ id: `weight-${index}`, date: dateAgo(days), weightKg: 74.2 + index * 0.2, waistCm: 80.5 })),
		workouts: empty ? [] : [0, 7, 14, 21].map((days, index) => ({
			id: `workout-${index}`, name: "Upper body", workoutDate: dateAgo(days), durationMinutes: 45,
			caloriesBurned: null, notes: null, createdAt: `${dateAgo(days)}T17:00:00Z`,
			exercises: [{ id: "exercise-bench", exerciseId: "exercise-bench", exerciseName: "Bench press", category: "STRENGTH", muscleGroup: "Chest", sortOrder: 0,
				sets: [1, 2, 3].map((setNumber) => ({ setNumber, reps: 8, weightKg: 60 - index * 2.5, durationSeconds: null, distanceMeters: null, completed: true })) }],
		})),
	};
}
function totals(entries: MealEntry[]) {
	return entries.reduce((sum, entry) => ({ calories: sum.calories + (entry.calories ?? 0), protein: sum.protein + (entry.proteinGrams ?? 0), carbs: sum.carbs + (entry.carbsGrams ?? 0), fat: sum.fat + (entry.fatGrams ?? 0), fiber: sum.fiber + (entry.fiberGrams ?? 0) }), { calories: 0, protein: 0, carbs: 0, fat: 0, fiber: 0 });
}
const goal = { id: "goal-1", targetCalories: 2200, targetProteinGrams: 140, targetCarbsGrams: 250, targetFatGrams: 70, targetWeight: 73, goalType: "maintenance", isActive: true, createdAt: `${dateAgo(30)}T12:00:00Z` };
function parameter(url: URL, name: string) {
	return [...url.searchParams].find(([key]) => key.toLowerCase() === name.toLowerCase())?.[1];
}
function page<T>(items: T[], url: URL) {
	const currentPage = Number(parameter(url, "page") ?? 1);
	const pageSize = Number(parameter(url, "pageSize") ?? 20);
	return { items: items.slice((currentPage - 1) * pageSize, currentPage * pageSize), totalCount: items.length, page: currentPage, pageSize, totalPages: Math.ceil(items.length / pageSize) };
}
function inRange(date: string, url: URL) {
	const from = parameter(url, "from");
	const to = parameter(url, "to");
	return (!from || date >= from) && (!to || date <= to);
}
function json(data: unknown, status = 200, headers: HeadersInit = {}) {
	return new Response(status === 204 ? null : JSON.stringify(data), { status, headers: { "Content-Type": "application/json", "Access-Control-Allow-Origin": webOrigin, "Access-Control-Allow-Credentials": "true", "Access-Control-Allow-Headers": "Content-Type,ngrok-skip-browser-warning", "Access-Control-Allow-Methods": "GET,POST,PUT,PATCH,DELETE,OPTIONS", ...headers } });
}
const server = Bun.serve({
	hostname: "127.0.0.1", port,
	async fetch(request) {
		const url = new URL(request.url);
		const path = url.pathname;
		const method = request.method;
		if (method === "OPTIONS") return json(null, 204);
		if (path === "/health") return json({ status: "fixture", today });
		if (path === "/__fixture/session" && method === "POST") {
			const options = await request.json() as { userId?: string; empty?: boolean; unread?: number; failures?: string[]; writeFailures?: Record<string, number> };
			const token = crypto.randomUUID();
			const state = initialState(options.empty);
			state.unread = options.unread ?? 0;
			state.failures = options.failures ?? [];
			state.writeFailures = options.writeFailures ?? {};
			state.userId = options.userId ?? user.id;
			states.set(token, state);
			return json({ user: { ...user, id: state.userId }, today }, 200, { "Set-Cookie": `mizan_session=${token}; Path=/; HttpOnly; SameSite=Lax` });
		}
		const token = request.headers.get("cookie")?.match(/(?:^|;\s*)mizan_session=([^;]+)/)?.[1];
		const state = token ? states.get(token) : undefined;
		if (!state) {
			if (method === "GET" && path === "/api/Recipes") return json(page(initialState().recipes.map((recipe) => ({ ...recipe, isOwner: false, isFavorited: false })), url));
			if (method === "GET" && path === "/api/Recipes/33333333-3333-4333-8333-333333333333") return json({ ...initialState().recipes[0], isOwner: false, isFavorited: false });
			return json({ error: "Fixture session required" }, 401);
		}
		if (path === "/__fixture/state") return json(state);
		if (state.failures.includes(path)) return json({ error: "This service is temporarily unavailable." }, 503);
		if (method !== "GET" && state.writeFailures[path] > 0) {
			state.writeFailures[path] -= 1;
			return json({ error: "Could not save this entry. Please try again." }, 503);
		}
		if (path === "/api/Auth/me") return json({ ...user, id: state.userId });
		if (path === "/api/Auth/logout") return json(null, 204, { "Set-Cookie": "mizan_session=; Path=/; Max-Age=0" });
		if (path === "/api/Subscriptions/me") return json({ isPro: false, plan: "Free", status: "none", isLifetime: false });
		if (path === "/api/Households/mine") return json({ households: [], activeHouseholdId: null });
		if (path === "/api/Notifications/unread-count") return json({ unreadCount: state.unread });
		if (path === "/api/Trainers/my-trainer") return json({ error: "No active trainer relationship found" }, 404);
		if (path === "/api/Goals") return state.empty ? json(null, 204) : json(goal);
		if (path === "/api/Goals/history") return json(state.empty ? [] : [goal]);
		if (path === "/api/Achievements/streak") return json({ currentStreak: 4, longestStreak: 12, isActiveToday: !state.empty });
		if (path === "/api/Foods/search") {
			const query = parameter(url, "searchTerm")?.toLowerCase() ?? "";
			return json(page(foods.filter((item) => item.name.toLowerCase().includes(query)), url));
		}
		if (path.startsWith("/api/Foods/")) return json(foods.find((item) => item.id === path.split("/").at(-1)) ?? null);
		if (path === "/api/Recipes") {
			const query = parameter(url, "searchTerm")?.toLowerCase() ?? "";
			const favoritesOnly = parameter(url, "favoritesOnly") === "true";
			return json(page(state.recipes.filter((item) => String(item.title).toLowerCase().includes(query) && (!favoritesOnly || item.isFavorited)), url));
		}
		if (path === "/api/Nutrition/log" && method === "POST") {
			const body = await request.json() as Record<string, unknown>;
			const selectedFood = foods.find((item) => item.id === body.foodId);
			const selectedRecipe = state.recipes.find((item) => item.id === body.recipeId);
			if (!(Number(body.servings) > 0) || !body.entryDate || (!selectedFood && !selectedRecipe)) return json({ error: "Choose a food or recipe and a positive quantity" }, 400);
			const multiplier = Number(body.servings);
			const nutrition = selectedRecipe?.nutrition as Record<string, number> | undefined;
			const entry: MealEntry = {
				id: crypto.randomUUID(), name: selectedFood?.name ?? String(selectedRecipe?.title),
				foodId: selectedFood?.id, recipeId: selectedRecipe ? String(selectedRecipe.id) : undefined,
				servings: multiplier, mealType: String(body.mealType), loggedAt: `${body.entryDate}T12:00:00Z`,
				calories: (selectedFood?.caloriesPer100g ?? nutrition?.caloriesPerServing ?? 0) * multiplier,
				proteinGrams: (selectedFood?.proteinPer100g ?? nutrition?.proteinGrams ?? 0) * multiplier,
				carbsGrams: (selectedFood?.carbsPer100g ?? nutrition?.carbsGrams ?? 0) * multiplier,
				fatGrams: (selectedFood?.fatPer100g ?? nutrition?.fatGrams ?? 0) * multiplier,
				fiberGrams: (selectedFood?.fiberPer100g ?? nutrition?.fiberGrams ?? 0) * multiplier,
			};
			state.meals.push(entry);
			state.writes.push({ path, body });
			return json({ id: entry.id, success: true, warnings: [] });
		}
		if (path === "/api/Recipes/promote" && method === "POST") {
			const body = await request.json() as Record<string, unknown>;
			const entries = state.meals.filter((item) => item.loggedAt.startsWith(String(body.entryDate)) && item.mealType === body.mealType);
			if (entries.length < 2 || !String(body.title ?? "").trim()) return json({ error: "A recipe needs at least two meal entries and a title." }, 400);
			const nutrition = totals(entries);
			const recipe = { id: crypto.randomUUID(), title: body.title, servings: 1, isOwner: true, isFavorited: false,
				nutrition: { caloriesPerServing: nutrition.calories, proteinGrams: nutrition.protein, carbsGrams: nutrition.carbs, fatGrams: nutrition.fat, fiberGrams: nutrition.fiber } };
			state.recipes.push(recipe);
			state.writes.push({ path, body });
			return json({ recipeId: recipe.id, id: recipe.id, success: true });
		}
		const recipePath = path.match(/^\/api\/Recipes\/([^/]+)(?:\/(favorite|preparation))?$/);
		if (recipePath) {
			const recipe = state.recipes.find((item) => item.id === recipePath[1]);
			if (!recipe) return json({ error: "Recipe not found" }, 404);
			if (method === "GET") return json(recipe);
			if (method === "POST" && recipePath[2] === "favorite") { recipe.isFavorited = !recipe.isFavorited; return json({ isFavorited: recipe.isFavorited }); }
			const body = await request.json() as Record<string, unknown>;
			if (method === "PUT") {
				const ingredients = body.ingredients as { foodId: string; amount: number; ingredientText: string; unit: string }[];
				if (!body.title || !(Number(body.servings) > 0) || !ingredients?.length) return json({ error: "Title, servings and ingredients are required" }, 400);
				Object.assign(recipe, body, { ingredients: ingredients.map((ingredient) => ({ ...ingredient, foodName: foods.find((item) => item.id === ingredient.foodId)?.name ?? ingredient.ingredientText })) });
				const nutrition = ingredients.reduce((sum, ingredient) => {
					const item = foods.find((candidate) => candidate.id === ingredient.foodId)!;
					const factor = ingredient.amount / 100 / Number(body.servings);
					return { caloriesPerServing: sum.caloriesPerServing + item.caloriesPer100g * factor, proteinGrams: sum.proteinGrams + item.proteinPer100g * factor, carbsGrams: sum.carbsGrams + item.carbsPer100g * factor, fatGrams: sum.fatGrams + item.fatPer100g * factor };
				}, { caloriesPerServing: 0, proteinGrams: 0, carbsGrams: 0, fatGrams: 0 });
				recipe.nutrition = nutrition;
				state.writes.push({ path, body });
				return json({ success: true });
			}
			if (method === "POST" && recipePath[2] === "preparation") {
				if (!(Number(body.yieldGrams) > 0)) return json({ error: "Finished batch weight is required" }, 400);
				state.writes.push({ path, body });
				return json({ foodId: "44444444-4444-4444-8444-444444444444", success: true });
			}
		}
		if (path === "/api/Meals/range") {
			const endDate = parameter(url, "endDate") ?? today;
			const days = Number(parameter(url, "days") ?? 7);
			return json({ days: Array.from({ length: days }, (_, index) => {
				const date = new Date(`${endDate}T12:00:00Z`);
				date.setUTCDate(date.getUTCDate() - days + index + 1);
				const day = date.toISOString().slice(0, 10);
				return { date: day, ...totals(state.meals.filter((meal) => meal.loggedAt.startsWith(day))) };
			}).filter((day) => state.meals.some((entry) => entry.loggedAt.startsWith(day.date))) });
		}
		if (path === "/api/Meals" && method === "GET") {
			const date = parameter(url, "date") ?? today;
			const entries = state.meals.filter((meal) => meal.loggedAt.startsWith(date));
			return json({ date, entries, totals: totals(entries) });
		}
		if (path === "/api/Meals" && method === "POST") {
			const body = await request.json() as Record<string, unknown>;
			if (!body.name || !(Number(body.servings) > 0) || !/^\d{4}-\d{2}-\d{2}$/.test(String(body.entryDate))) return json({ error: "Name, servings and entryDate are required" }, 400);
			const entry: MealEntry = { ...(body as unknown as MealEntry), id: crypto.randomUUID(), loggedAt: `${body.entryDate}T12:00:00Z` };
			state.meals.push(entry);
			state.writes.push({ path, body });
			return json({ id: entry.id, success: true, warnings: [] });
		}
		if (path === "/api/BodyMeasurements" && method === "GET") return json(page(state.measurements.filter((item) => inRange(item.date, url)), url));
		if (path === "/api/BodyMeasurements" && method === "POST") {
			const body = await request.json() as Record<string, unknown>;
			if (!body.date || !Object.entries(body).some(([key, value]) => key !== "date" && key !== "notes" && Number(value) > 0)) return json({ error: "Date and a positive measurement are required" }, 400);
			const entry = { ...(body as unknown as BodyMeasurement), id: crypto.randomUUID() };
			state.measurements.unshift(entry);
			state.writes.push({ path, body });
			return json({ id: entry.id, success: true });
		}
		if (path === "/api/Workouts/draft") {
			if (method === "GET") return state.draft ? json(state.draft) : json({ error: "No draft" }, 404);
			if (method === "PUT") { state.draft = { ...await request.json() as { payload: string }, updatedAt: new Date().toISOString() }; return json(null, 204); }
			if (method === "DELETE") { state.draft = null; return json(null, 204); }
		}
		if (path === "/api/Workouts" && method === "GET") return json(page(state.workouts.filter((item) => inRange(item.workoutDate, url)), url));
		if (path === "/api/Workouts" && method === "POST") {
			const body = await request.json() as { name: string; workoutDate: string; exercises: { exerciseId: string; sets: WorkoutSummary["exercises"][number]["sets"] }[] };
			if (!body.workoutDate || !body.exercises?.length) return json({ error: "A workout date and exercise are required" }, 400);
			const workout: WorkoutSummary = { id: crypto.randomUUID(), name: body.name, workoutDate: body.workoutDate, durationMinutes: 1, caloriesBurned: null, notes: null, createdAt: new Date().toISOString(),
				exercises: body.exercises.map((exercise) => ({ id: crypto.randomUUID(), exerciseId: exercise.exerciseId, exerciseName: "Bench press", category: "Strength", muscleGroup: "Chest", sortOrder: 0, sets: exercise.sets.map((set, index) => ({ ...set, setNumber: index + 1 })) })) };
			state.workouts.unshift(workout);
			state.writes.push({ path, body });
			return json({ id: workout.id, totalExercises: workout.exercises.length, totalSets: workout.exercises.reduce((sum, exercise) => sum + exercise.sets.filter((set) => set.completed).length, 0), personalRecords: [] });
		}
		if (path === "/api/Exercises") return json(page([{ id: "exercise-bench", name: "Bench press", category: "STRENGTH", muscleGroup: "Chest", isPublic: true }], url));
		if (path === "/api/WorkoutTemplates") return json([{ id: "template-strength", name: "Simple strength", programName: "Weekly training", exercises: [], isBuiltIn: true, sessionOrder: 0, sortOrder: 0 }]);
		if (path === "/api/WorkoutTemplates/template-strength/next-session") return json({ templateId: "template-strength", name: "Simple strength", exercises: [{ exerciseId: "exercise-bench", name: "Bench press", category: "Strength", supersetWithNext: false, restSecondsMin: 60, restSecondsMax: 120, sets: [{ targetReps: 8, weightKg: 55 }] }] });
		if (path === "/api/Social/profile") return json({ error: "No social profile" }, 404);
		if (method === "DELETE") {
			const id = path.split("/").at(-1);
			if (path.startsWith("/api/Meals/")) state.meals = state.meals.filter((item) => item.id !== id);
			else if (path.startsWith("/api/Workouts/")) state.workouts = state.workouts.filter((item) => item.id !== id);
			else if (path.startsWith("/api/BodyMeasurements/")) state.measurements = state.measurements.filter((item) => item.id !== id);
			else { state.unhandled.push(`${method} ${path}`); return json({ error: "Unsupported delete" }, 501); }
			state.writes.push({ path, body: {} });
			return json(null, 204);
		}
		state.unhandled.push(`${method} ${path}`);
		return json({ error: `Fixture does not implement ${method} ${path}` }, 501);
	},
});
console.log(`UI fixture API listening on http://localhost:${server.port}`);
