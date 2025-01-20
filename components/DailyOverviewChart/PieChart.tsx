
"use client";

import { PieChart, Pie, Tooltip, Legend, Cell, ResponsiveContainer } from 'recharts';
import { useEffect, useState } from 'react';
import Macros from '@/types/macro';

interface OverviewPieChartProps {
	macros: Macros;
}

export default function OverviewPieChart({ macros }: OverviewPieChartProps) {
	
	

	// Data for the pie chart
	const data = [
		{ name: 'Protein', value: macros.protein },
		{ name: 'Carbs', value: macros.carbs },
		{ name: 'Fat', value: macros.fat },
	];

	const COLORS = ['#8884d8', '#82ca9d', '#ffc658'];

	return (
		<div className="w-full h-72">
			<ResponsiveContainer width="100%" height="100%">
				<PieChart>
					<Pie
						data={data}
						dataKey="value"
						nameKey="name"
						cx="50%"
						cy="50%"
						outerRadius="80%"
						label
					>
						{data.map((entry, index) => (
							<Cell key={`cell-${index}`} fill={COLORS[index % COLORS.length]} />
						))}
					</Pie>
					<Tooltip />
					<Legend verticalAlign="bottom" height={24} />
				</PieChart>
			</ResponsiveContainer>
		</div>
	)
}
