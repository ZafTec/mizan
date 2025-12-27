"use client";

import { useState, useRef } from "react";
import { apiClient } from "@/lib/auth-client";

interface RecognizedFood {
	name: string;
	portionGrams: number;
	calories: number;
	protein: number;
	carbs: number;
	fat: number;
}

interface AnalysisResult {
	foods: RecognizedFood[];
	totalCalories: number;
	confidence: number;
}

export function FoodImageAnalyzer() {
	const [selectedImage, setSelectedImage] = useState<string | null>(null);
	const [isAnalyzing, setIsAnalyzing] = useState(false);
	const [result, setResult] = useState<AnalysisResult | null>(null);
	const [error, setError] = useState<string | null>(null);
	const fileInputRef = useRef<HTMLInputElement>(null);

	const handleImageSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
		const file = e.target.files?.[0];
		if (file) {
			const reader = new FileReader();
			reader.onloadend = () => {
				setSelectedImage(reader.result as string);
				setResult(null);
				setError(null);
			};
			reader.readAsDataURL(file);
		}
	};

	const handleAnalyze = async () => {
		if (!selectedImage || !fileInputRef.current?.files?.[0]) return;

		setIsAnalyzing(true);
		setError(null);

		try {
			const formData = new FormData();
			formData.append("image", fileInputRef.current.files[0]);

			const response = await fetch(
				`${process.env["NEXT_PUBLIC_API_URL"]}/api/nutrition/ai/analyze-image`,
				{
					method: "POST",
					body: formData,
					headers: {
						Authorization: `Bearer ${await getToken()}`,
					},
				}
			);

			if (!response.ok) {
				throw new Error("Failed to analyze image");
			}

			const data: AnalysisResult = await response.json();
			setResult(data);
		} catch (err) {
			console.error("Failed to analyze image:", err);
			setError("Failed to analyze image. Please try again.");
		} finally {
			setIsAnalyzing(false);
		}
	};

	const handleLogFood = async (food: RecognizedFood) => {
		try {
			await apiClient("/api/nutrition/log", {
				method: "POST",
				body: JSON.stringify({
					foodName: food.name,
					servings: food.portionGrams / 100,
					calories: food.calories,
					proteinGrams: food.protein,
					carbsGrams: food.carbs,
					fatGrams: food.fat,
					mealType: getMealType(),
					entryDate: new Date().toISOString().split("T")[0],
				}),
			});

			// Show success feedback
			alert(`Logged ${food.name} successfully!`);
		} catch (err) {
			console.error("Failed to log food:", err);
			alert("Failed to log food. Please try again.");
		}
	};

	const getMealType = (): string => {
		const hour = new Date().getHours();
		if (hour < 10) return "breakfast";
		if (hour < 14) return "lunch";
		if (hour < 17) return "snack";
		return "dinner";
	};

	return (
		<div className="bg-white rounded-lg shadow-md p-6">
			<h2 className="text-xl font-semibold mb-4 flex items-center">
				<i className="ri-camera-line mr-2 text-green-600"></i>
				Food Image Analyzer
			</h2>

			<p className="text-gray-600 text-sm mb-4">
				Take a photo of your meal and let AI identify the foods and estimate
				their nutritional content.
			</p>

			{/* Upload Area */}
			<div
				className={`border-2 border-dashed rounded-lg p-6 text-center cursor-pointer transition-colors ${selectedImage
						? "border-green-500 bg-green-50"
						: "border-gray-300 hover:border-green-400"
					}`}
				onClick={() => fileInputRef.current?.click()}
			>
				{selectedImage ? (
					<img
						src={selectedImage}
						alt="Selected food"
						className="max-h-64 mx-auto rounded-lg"
					/>
				) : (
					<div className="py-8">
						<i className="ri-image-add-line text-4xl text-gray-400 mb-2"></i>
						<p className="text-gray-500">
							Click to upload or drag and drop an image
						</p>
						<p className="text-xs text-gray-400 mt-1">
							Supports JPG, PNG up to 10MB
						</p>
					</div>
				)}
				<input
					ref={fileInputRef}
					type="file"
					accept="image/*"
					onChange={handleImageSelect}
					className="hidden"
				/>
			</div>

			{/* Analyze Button */}
			{selectedImage && (
				<button
					onClick={handleAnalyze}
					disabled={isAnalyzing}
					className="w-full mt-4 py-2 bg-green-600 text-white rounded-lg hover:bg-green-700 disabled:opacity-50 flex items-center justify-center"
				>
					{isAnalyzing ? (
						<>
							<i className="ri-loader-4-line animate-spin mr-2"></i>
							Analyzing...
						</>
					) : (
						<>
							<i className="ri-search-eye-line mr-2"></i>
							Analyze Food
						</>
					)}
				</button>
			)}

			{/* Error */}
			{error && (
				<div className="mt-4 p-3 bg-red-50 text-red-600 rounded-lg text-sm">
					{error}
				</div>
			)}

			{/* Results */}
			{result && (
				<div className="mt-6">
					<div className="flex items-center justify-between mb-4">
						<h3 className="font-semibold">Analysis Results</h3>
						<span className="text-sm text-gray-500">
							Confidence: {(result.confidence * 100).toFixed(0)}%
						</span>
					</div>

					<div className="space-y-3">
						{result.foods.map((food, index) => (
							<div
								key={index}
								className="p-3 bg-gray-50 rounded-lg flex items-center justify-between"
							>
								<div>
									<p className="font-medium">{food.name}</p>
									<p className="text-sm text-gray-500">
										{food.portionGrams}g • {food.calories} kcal
									</p>
									<p className="text-xs text-gray-400">
										P: {food.protein}g • C: {food.carbs}g • F: {food.fat}g
									</p>
								</div>
								<button
									onClick={() => handleLogFood(food)}
									className="px-3 py-1 bg-green-600 text-white text-sm rounded-lg hover:bg-green-700"
								>
									Log
								</button>
							</div>
						))}
					</div>

					<div className="mt-4 p-3 bg-green-50 rounded-lg">
						<p className="font-semibold text-green-800">
							Total: {result.totalCalories} calories
						</p>
					</div>

					<button
						onClick={() => {
							result.foods.forEach(handleLogFood);
						}}
						className="w-full mt-4 py-2 bg-green-600 text-white rounded-lg hover:bg-green-700"
					>
						Log All Foods
					</button>
				</div>
			)}
		</div>
	);
}

async function getToken(): Promise<string> {
	const { getApiToken } = await import("@/lib/auth-client");
	return (await getApiToken()) || "";
}
