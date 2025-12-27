'use client';

import Loading from "@/components/Loading";
import { useEffect, useState } from "react";

export default function RegenerateLoading() {
  const [loadingMessage, setLoadingMessage] = useState("Regenerating recipe suggestions...");

  // Cycle through different loading messages
  useEffect(() => {
    const messages = [
      "Regenerating recipe suggestions...",
      "Analyzing your nutritional needs...",
      "Finding the best combinations...",
      "Balancing your macros...",
      "Preparing fresh ideas for you..."
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
      <h1 className="text-3xl font-bold mb-8 text-gray-800">Regenerating Recipe Suggestions</h1>
      
      <div className="flex flex-col justify-center items-center py-16">
        <Loading />
        <p className="text-gray-600 mb-8 animate-pulse">
            {loadingMessage}
        </p>
      </div>
    </div>
  );
}
