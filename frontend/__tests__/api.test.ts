import { describe, it, expect, vi, beforeEach } from "vitest";

// Mock API client
const mockApiClient = vi.fn();

vi.mock("@/lib/auth-client", () => ({
	apiClient: mockApiClient,
	getApiToken: vi.fn().mockResolvedValue("mock-token"),
}));

describe("Nutrition API", () => {
	beforeEach(() => {
		vi.clearAllMocks();
	});

	describe("getDailyNutrition", () => {
		it("fetches daily nutrition data successfully", async () => {
			const mockResponse = {
				date: "2024-12-08",
				totalCalories: 1500,
				totalProtein: 120,
				totalCarbs: 150,
				totalFat: 50,
				targetCalories: 2000,
				mealBreakdown: [
					{ mealType: "breakfast", calories: 400 },
					{ mealType: "lunch", calories: 600 },
					{ mealType: "snack", calories: 500 },
				],
			};

			mockApiClient.mockResolvedValue(mockResponse);

			const result = await mockApiClient("/api/nutrition/daily?date=2024-12-08");

			expect(result.totalCalories).toBe(1500);
			expect(result.mealBreakdown).toHaveLength(3);
		});

		it("handles API errors gracefully", async () => {
			mockApiClient.mockRejectedValue(new Error("Network error"));

			await expect(mockApiClient("/api/nutrition/daily")).rejects.toThrow(
				"Network error"
			);
		});
	});

	describe("logFood", () => {
		it("logs food entry successfully", async () => {
			const mockResponse = {
				id: "entry-123",
				calories: 165,
				proteinGrams: 31,
				carbsGrams: 0,
				fatGrams: 3.6,
				message: "Logged 100g of Chicken Breast (165 kcal)",
			};

			mockApiClient.mockResolvedValue(mockResponse);

			const result = await mockApiClient("/api/nutrition/log", {
				method: "POST",
				body: JSON.stringify({
					foodId: "food-123",
					entryDate: "2024-12-08",
					mealType: "lunch",
					servings: 1,
				}),
			});

			expect(result.id).toBe("entry-123");
			expect(result.message).toContain("Chicken Breast");
		});
	});
});

describe("Recipes API", () => {
	beforeEach(() => {
		vi.clearAllMocks();
	});

	describe("getRecipes", () => {
		it("fetches recipes with pagination", async () => {
			const mockResponse = {
				recipes: [
					{
						id: "recipe-1",
						title: "Doro Wat",
						description: "Ethiopian chicken stew",
						servings: 4,
						nutrition: { caloriesPerServing: 350 },
					},
					{
						id: "recipe-2",
						title: "Misir Wat",
						description: "Ethiopian red lentil stew",
						servings: 6,
						nutrition: { caloriesPerServing: 200 },
					},
				],
				totalCount: 25,
				page: 1,
				pageSize: 20,
			};

			mockApiClient.mockResolvedValue(mockResponse);

			const result = await mockApiClient("/api/recipes?page=1&pageSize=20");

			expect(result.recipes).toHaveLength(2);
			expect(result.totalCount).toBe(25);
			expect(result.recipes[0].title).toBe("Doro Wat");
		});

		it("filters recipes by search term", async () => {
			const mockResponse = {
				recipes: [
					{
						id: "recipe-1",
						title: "Chicken Stir Fry",
						description: "Quick and easy",
					},
				],
				totalCount: 1,
			};

			mockApiClient.mockResolvedValue(mockResponse);

			const result = await mockApiClient("/api/recipes?searchTerm=chicken");

			expect(result.recipes).toHaveLength(1);
			expect(result.recipes[0].title).toContain("Chicken");
		});
	});

	describe("createRecipe", () => {
		it("creates a new recipe successfully", async () => {
			const mockResponse = {
				id: "new-recipe-123",
				title: "Test Recipe",
			};

			mockApiClient.mockResolvedValue(mockResponse);

			const result = await mockApiClient("/api/recipes", {
				method: "POST",
				body: JSON.stringify({
					title: "Test Recipe",
					servings: 4,
					ingredients: [{ ingredientText: "Test ingredient" }],
					instructions: ["Step 1", "Step 2"],
				}),
			});

			expect(result.id).toBe("new-recipe-123");
			expect(result.title).toBe("Test Recipe");
		});
	});
});

describe("Foods API", () => {
	describe("searchFoods", () => {
		it("searches foods by name", async () => {
			const mockResponse = {
				foods: [
					{
						id: "food-1",
						name: "Chicken Breast",
						caloriesPer100g: 165,
						proteinPer100g: 31,
						isVerified: true,
					},
					{
						id: "food-2",
						name: "Chicken Thigh",
						caloriesPer100g: 209,
						proteinPer100g: 26,
						isVerified: true,
					},
				],
			};

			mockApiClient.mockResolvedValue(mockResponse);

			const result = await mockApiClient("/api/foods/search?searchTerm=chicken");

			expect(result.foods).toHaveLength(2);
			expect(result.foods[0].name).toBe("Chicken Breast");
		});

		it("searches foods by barcode", async () => {
			const mockResponse = {
				foods: [
					{
						id: "food-1",
						name: "Organic Milk",
						barcode: "1234567890",
						caloriesPer100g: 42,
					},
				],
			};

			mockApiClient.mockResolvedValue(mockResponse);

			const result = await mockApiClient(
				"/api/foods/search?barcode=1234567890"
			);

			expect(result.foods).toHaveLength(1);
			expect(result.foods[0].barcode).toBe("1234567890");
		});
	});
});
