'use client';

import Loading from "@/components/Loading";
import { useEffect, useState } from "react";

export default function SuggestionsLoading() {
  const [loadingMessage, setLoadingMessage] = useState("Finding recipes that match your goals...");

  // Cycle through different loading messages
	useEffect(() => {
		const messages = [
			"Finding recipes that match your goals...",
			"Analyzing your ingredient preferences...",
			"Calculating nutritional balance...",
			"Personalizing recipe suggestions...",
			"Almost there..."
		];
		
		let currentIndex = 0;
		const interval = setInterval(() => {
			currentIndex = (currentIndex + 1) % messages.length;
			setLoadingMessage(messages[currentIndex]);
		}, 2000);
		
		return () => clearInterval(interval);
	}, []);

	return (
		<div className="max-w-7xl mx-auto px-4 py-8">
			<h1 className="text-3xl font-bold mb-8 text-gray-800">Recipe Suggestions</h1>
			
			<div className="flex flex-col justify-center items-center py-16">
				<Loading />
				<p className="text-gray-600 mb-8 animate-pulse">
					{loadingMessage}
				</p>
			</div>
		</div>
	);
}
