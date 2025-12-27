
"use client";

import { PerDayMealsAggregate } from "@/types/meal";
import {
	AreaChart,
	Area,
	XAxis,
	YAxis,
	CartesianGrid,
	Tooltip,
	ResponsiveContainer,
	Legend,
	LineChart,
	Line,
	Brush,
} from "recharts";

interface MealMacrosChartProps {
  perDayMeals: PerDayMealsAggregate[];
}

export default function MealMacrosChart({ perDayMeals }: MealMacrosChartProps) {
  // Prepare data for the chart
	const data = perDayMeals.map((perDayMeal) => {
		const dateStr = perDayMeal.date.toISOString().substring(0, 10); // 'YYYY-MM-DD'
		const totalMacros =
			perDayMeal.totalMacros.protein +
			perDayMeal.totalMacros.carbs +
			perDayMeal.totalMacros.fat +
			perDayMeal.totalMacros.fiber;

		// Calculate percentages for each macro
		const proteinPercent = totalMacros > 0 ? perDayMeal.totalMacros.protein / totalMacros : 0;
		const carbsPercent = totalMacros > 0 ? perDayMeal.totalMacros.carbs / totalMacros : 0;
		const fatPercent = totalMacros > 0 ? perDayMeal.totalMacros.fat / totalMacros : 0;
		const fiberPercent = totalMacros > 0 ? perDayMeal.totalMacros.fiber / totalMacros : 0;

		return {
			date: dateStr,
			calories: perDayMeal.totalMacros.calories,
			protein: perDayMeal.totalMacros.protein,
			carbs: perDayMeal.totalMacros.carbs,
			fat: perDayMeal.totalMacros.fat,
			fiber: perDayMeal.totalMacros.fiber,
			proteinPercent,
			carbsPercent,
			fatPercent,
			fiberPercent,
		};
	});

	// Function to format percentages
	const toPercent = (decimal: number, fixed = 1) => `${(decimal * 100).toFixed(1)}%`;

	// Custom tooltip content for the percentage area chart
	const renderTooltipContent = (o: any) => {
		const { payload, label } = o;
		if (!payload || payload.length === 0) {
			return null;
		}
		const entry = payload[0].payload;
		return (
			<div className="customized-tooltip-content">
				<p className="label">{`${label}`}</p>
				<ul className="list">
					<li style={{ color: '#8884d8' }}>
						Protein: {toPercent(entry.proteinPercent, 1)} ({entry.protein}g)
					</li>
					<li style={{ color: '#82ca9d' }}>
						Carbs: {toPercent(entry.carbsPercent, 1)} ({entry.carbs}g)
					</li>
					<li style={{ color: '#ffc658' }}>
						Fat: {toPercent(entry.fatPercent, 1)} ({entry.fat}g)
					</li>
					<li style={{ color: '#a4de6c' }}>
						Fiber: {toPercent(entry.fiberPercent, 1)} ({entry.fiber}g)
					</li>
				</ul>
			</div>
		)
	}

	return (
		<div style={{ width: "100%", height: 450 }}>
			<h3>Macros Percentage Over Time</h3>
			<ResponsiveContainer width="100%" height={200}>
				<AreaChart
					data={data}
					stackOffset="expand"
					margin={{
						top: 10,
						right: 30,
						left: 20,
						bottom: 0,
					}}
					syncId="mealChart" // Synchronize charts
				>
					<CartesianGrid strokeDasharray="3 3" />
					<XAxis dataKey="date" />
					<YAxis tickFormatter={toPercent} />
					<Tooltip content={renderTooltipContent} />
					<Legend />
					<Area
						type="monotone"
						dataKey="proteinPercent"
						name="Protein"
						stackId="1"
						stroke="#8884d8"
						fill="#8884d8"
					/>
					<Area
						type="monotone"
						dataKey="carbsPercent"
						name="Carbs"
						stackId="1"
						stroke="#82ca9d"
						fill="#82ca9d"
					/>
					<Area
						type="monotone"
						dataKey="fatPercent"
						name="Fat"
						stackId="1"
						stroke="#ffc658"
						fill="#ffc658"
					/>
					<Area
						type="monotone"
						dataKey="fiberPercent"
						name="Fiber"
						stackId="1"
						stroke="#a4de6c"
						fill="#a4de6c"
					/>
				</AreaChart>
			</ResponsiveContainer>

			<h3>Calories Over Time</h3>
			<ResponsiveContainer width="100%" height={200}>
				<LineChart
					data={data}
					syncId="mealChart" // Synchronize charts
					margin={{
						top: 10,
						right: 30,
						left: 20,
						bottom: 0,
					}}
				>
					<CartesianGrid strokeDasharray="3 3" />
					<XAxis dataKey="date" />
					<YAxis />
					<Tooltip formatter={(value: any) => `${value} kcal`} />
					<Legend />
					<Line
						type="monotone"
						dataKey="calories"
						name="Calories"
						stroke="#ff7300"
						fill="#ff7300"
					/>
					<Brush />
				</LineChart>
			</ResponsiveContainer>
		</div>
	)
}
