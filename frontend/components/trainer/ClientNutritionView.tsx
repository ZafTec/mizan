"use client";

import { useEffect, useState } from "react";
import { clientApi } from "@/lib/api.client";
import { Calendar } from "@/components/ui/calendar";
import { Button } from "@/components/ui/button";
import {
	Popover,
	PopoverContent,
	PopoverTrigger,
} from "@/components/ui/popover";
import { CalendarIcon, ChevronLeft, ChevronRight } from "lucide-react";
import { format } from "date-fns";
import { useToast } from "@/lib/hooks/use-toast";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
	BarChart,
	Bar,
	XAxis,
	YAxis,
	CartesianGrid,
	Tooltip,
	ResponsiveContainer,
	PieChart,
	Pie,
	Cell,
} from "recharts";

interface FoodLogEntry {
	id: string;
	foodName: string;
	servingSize: number;
	servingUnit?: string;
	calories: number;
	protein: number;
	carbs: number;
	fat: number;
	loggedAt: string;
}

interface NutritionSummary {
	totalCalories: number;
	totalProtein: number;
	totalCarbs: number;
	totalFat: number;
}

interface ClientNutritionData {
	clientId: string;
	date: string;
	foodLogs: FoodLogEntry[];
	summary: NutritionSummary;
}

interface ClientNutritionViewProps {
	clientId: string;
}

const COLORS = {
	protein: "#3b82f6",
	carbs: "#10b981",
	fat: "#f59e0b",
};

export function ClientNutritionView({ clientId }: ClientNutritionViewProps) {
	const [date, setDate] = useState<Date>(new Date());
	const [nutritionData, setNutritionData] = useState<ClientNutritionData | null>(
		null
	);
	const [loading, setLoading] = useState(true);
	const { toast } = useToast();

	async function fetchNutritionData(selectedDate: Date) {
		setLoading(true);
		try {
			const dateStr = format(selectedDate, "yyyy-MM-dd");
			const data = await clientApi<ClientNutritionData>(
				`/api/Trainers/clients/${clientId}/nutrition?date=${dateStr}`
			);
			setNutritionData(data);
		} catch (error: any) {
			console.error("Failed to fetch nutrition data:", error);
			if (error.status === 401) {
				toast({
					title: "Permission Denied",
					description: "You don't have permission to view this client's nutrition data",
					variant: "destructive",
				});
			} else if (error.status === 404) {
				setNutritionData(null);
			} else {
				toast({
					title: "Error",
					description: "Failed to load nutrition data",
					variant: "destructive",
				});
			}
		} finally {
			setLoading(false);
		}
	}

	useEffect(() => {
		fetchNutritionData(date);
	}, [clientId, date]);

	function handlePreviousDay() {
		const newDate = new Date(date);
		newDate.setDate(newDate.getDate() - 1);
		setDate(newDate);
	}

	function handleNextDay() {
		const newDate = new Date(date);
		newDate.setDate(newDate.getDate() + 1);
		setDate(newDate);
	}

	const macroData = nutritionData
		? [
				{ name: "Protein", value: nutritionData.summary.totalProtein, color: COLORS.protein },
				{ name: "Carbs", value: nutritionData.summary.totalCarbs, color: COLORS.carbs },
				{ name: "Fat", value: nutritionData.summary.totalFat, color: COLORS.fat },
		  ]
		: [];

	const barData = nutritionData
		? [
				{
					name: "Macros",
					Protein: nutritionData.summary.totalProtein,
					Carbs: nutritionData.summary.totalCarbs,
					Fat: nutritionData.summary.totalFat,
				},
		  ]
		: [];

	if (loading) {
		return (
			<div className="animate-pulse space-y-4">
				<div className="h-12 bg-gray-200 rounded w-1/3"></div>
				<div className="h-64 bg-gray-200 rounded"></div>
			</div>
		);
	}

	return (
		<div className="space-y-6">
			<div className="flex items-center justify-between">
				<h2 className="text-2xl font-bold">Nutrition Data</h2>

				<div className="flex items-center space-x-2">
					<Button variant="outline" size="icon" onClick={handlePreviousDay}>
						<ChevronLeft className="h-4 w-4" />
					</Button>

					<Popover>
						<PopoverTrigger asChild>
							<Button variant="outline">
								<CalendarIcon className="mr-2 h-4 w-4" />
								{format(date, "PPP")}
							</Button>
						</PopoverTrigger>
						<PopoverContent className="w-auto p-0" align="end">
							<Calendar
								mode="single"
								selected={date}
								onSelect={(newDate) => newDate && setDate(newDate)}
								initialFocus
							/>
						</PopoverContent>
					</Popover>

					<Button
						variant="outline"
						size="icon"
						onClick={handleNextDay}
						disabled={format(date, "yyyy-MM-dd") >= format(new Date(), "yyyy-MM-dd")}
					>
						<ChevronRight className="h-4 w-4" />
					</Button>
				</div>
			</div>

			{!nutritionData || nutritionData.foodLogs.length === 0 ? (
				<Card>
					<CardContent className="pt-6">
						<p className="text-center text-gray-500 py-8">
							No nutrition data for this date
						</p>
					</CardContent>
				</Card>
			) : (
				<>
					<div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
						<Card>
							<CardHeader className="pb-2">
								<CardTitle className="text-sm font-medium text-gray-600">
									Calories
								</CardTitle>
							</CardHeader>
							<CardContent>
								<p className="text-3xl font-bold">
									{Math.round(nutritionData.summary.totalCalories)}
								</p>
								<p className="text-xs text-gray-500">kcal</p>
							</CardContent>
						</Card>

						<Card>
							<CardHeader className="pb-2">
								<CardTitle className="text-sm font-medium text-gray-600">
									Protein
								</CardTitle>
							</CardHeader>
							<CardContent>
								<p className="text-3xl font-bold text-blue-600">
									{Math.round(nutritionData.summary.totalProtein)}g
								</p>
								<p className="text-xs text-gray-500">
									{Math.round((nutritionData.summary.totalProtein * 4 / nutritionData.summary.totalCalories) * 100)}% of calories
								</p>
							</CardContent>
						</Card>

						<Card>
							<CardHeader className="pb-2">
								<CardTitle className="text-sm font-medium text-gray-600">
									Carbs
								</CardTitle>
							</CardHeader>
							<CardContent>
								<p className="text-3xl font-bold text-green-600">
									{Math.round(nutritionData.summary.totalCarbs)}g
								</p>
								<p className="text-xs text-gray-500">
									{Math.round((nutritionData.summary.totalCarbs * 4 / nutritionData.summary.totalCalories) * 100)}% of calories
								</p>
							</CardContent>
						</Card>

						<Card>
							<CardHeader className="pb-2">
								<CardTitle className="text-sm font-medium text-gray-600">
									Fat
								</CardTitle>
							</CardHeader>
							<CardContent>
								<p className="text-3xl font-bold text-orange-600">
									{Math.round(nutritionData.summary.totalFat)}g
								</p>
								<p className="text-xs text-gray-500">
									{Math.round((nutritionData.summary.totalFat * 9 / nutritionData.summary.totalCalories) * 100)}% of calories
								</p>
							</CardContent>
						</Card>
					</div>

					<div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
						<Card>
							<CardHeader>
								<CardTitle>Macro Distribution</CardTitle>
							</CardHeader>
							<CardContent>
								<ResponsiveContainer width="100%" height={300}>
									<PieChart>
										<Pie
											data={macroData}
											cx="50%"
											cy="50%"
											labelLine={false}
											label={({ name, value }) => `${name}: ${Math.round(value)}g`}
											outerRadius={80}
											fill="#8884d8"
											dataKey="value"
										>
											{macroData.map((entry, index) => (
												<Cell key={`cell-${index}`} fill={entry.color} />
											))}
										</Pie>
										<Tooltip />
									</PieChart>
								</ResponsiveContainer>
							</CardContent>
						</Card>

						<Card>
							<CardHeader>
								<CardTitle>Macros Breakdown</CardTitle>
							</CardHeader>
							<CardContent>
								<ResponsiveContainer width="100%" height={300}>
									<BarChart data={barData}>
										<CartesianGrid strokeDasharray="3 3" />
										<XAxis dataKey="name" />
										<YAxis />
										<Tooltip />
										<Bar dataKey="Protein" fill={COLORS.protein} />
										<Bar dataKey="Carbs" fill={COLORS.carbs} />
										<Bar dataKey="Fat" fill={COLORS.fat} />
									</BarChart>
								</ResponsiveContainer>
							</CardContent>
						</Card>
					</div>

					<Card>
						<CardHeader>
							<CardTitle>Food Log</CardTitle>
						</CardHeader>
						<CardContent>
							<div className="space-y-3">
								{nutritionData.foodLogs.map((entry) => (
									<div
										key={entry.id}
										className="flex items-center justify-between p-3 bg-gray-50 rounded-lg"
									>
										<div>
											<p className="font-medium">{entry.foodName}</p>
											<p className="text-sm text-gray-500">
												{entry.servingSize} {entry.servingUnit} •{" "}
												{new Date(entry.loggedAt).toLocaleTimeString()}
											</p>
										</div>
										<div className="text-right">
											<p className="font-semibold">
												{Math.round(entry.calories)} cal
											</p>
											<p className="text-xs text-gray-500">
												P: {Math.round(entry.protein)}g • C:{" "}
												{Math.round(entry.carbs)}g • F: {Math.round(entry.fat)}g
											</p>
										</div>
									</div>
								))}
							</div>
						</CardContent>
					</Card>
				</>
			)}
		</div>
	);
}
