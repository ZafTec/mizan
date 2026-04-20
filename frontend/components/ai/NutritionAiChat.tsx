"use client";

import { useState, useRef, useEffect } from "react";
import { clientApi } from "@/lib/api.client";

interface Message {
	id: string;
	role: "user" | "assistant";
	content: string;
	timestamp: Date;
}

export function NutritionAiChat() {
	const [messages, setMessages] = useState<Message[]>([
		{
			id: "welcome",
			role: "assistant",
			content:
				"ሰላም! 👋 I'm Mizan AI, your nutrition and fitness coach. I can help you:\n\n• Log your meals and track nutrition\n• Get information about foods\n• Suggest recipes based on your remaining macros\n• Answer questions about nutrition\n\nHow can I help you today?",
			timestamp: new Date(),
		},
	]);
	const [input, setInput] = useState("");
	const [isLoading, setIsLoading] = useState(false);
	const messagesEndRef = useRef<HTMLDivElement>(null);

	const scrollToBottom = () => {
		messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
	};

	useEffect(() => {
		scrollToBottom();
	}, [messages]);

	const handleSend = async () => {
		if (!input.trim() || isLoading) return;

		const userMessage: Message = {
			id: `user-${Date.now()}`,
			role: "user",
			content: input.trim(),
			timestamp: new Date(),
		};

		setMessages((prev) => [...prev, userMessage]);
		setInput("");
		setIsLoading(true);

		try {
			const response = await clientApi<{ response: string }>(
				"/api/nutrition/ai/chat",
				{
					method: "POST",
					body: { message: userMessage.content },
				}
			);

			const assistantMessage: Message = {
				id: `assistant-${Date.now()}`,
				role: "assistant",
				content: response.response,
				timestamp: new Date(),
			};

			setMessages((prev) => [...prev, assistantMessage]);
		} catch (error) {
			console.error("Failed to get AI response:", error);

			const errorMessage: Message = {
				id: `error-${Date.now()}`,
				role: "assistant",
				content:
					"I apologize, but I encountered an error. Please try again later.",
				timestamp: new Date(),
			};

			setMessages((prev) => [...prev, errorMessage]);
		} finally {
			setIsLoading(false);
		}
	};

	const handleKeyPress = (e: React.KeyboardEvent) => {
		if (e.key === "Enter" && !e.shiftKey) {
			e.preventDefault();
			handleSend();
		}
	};

	const quickActions = [
		{ label: "Log my meal", prompt: "I just had " },
		{ label: "Today's summary", prompt: "What's my nutrition summary for today?" },
		{ label: "Recipe ideas", prompt: "Suggest some recipes based on my remaining macros" },
		{ label: "Food info", prompt: "What's the nutrition info for " },
	];

	return (
		<div className="flex flex-col h-150 bg-white dark:bg-charcoal-blue-900 rounded-lg shadow-md">
			{/* Header */}
			<div className="flex items-center space-x-3 p-4 border-b bg-green-700 text-white rounded-t-lg dark:bg-green-800">
				<div className="w-10 h-10 bg-white/20 rounded-2xl flex items-center justify-center">
					<span className="text-xl">🤖</span>
				</div>
				<div>
					<h3 className="font-semibold">Mizan AI</h3>
					<p className="text-xs text-green-100">
						Your nutrition & fitness coach
					</p>
				</div>
			</div>

			{/* Quick Actions */}
			<div className="flex flex-wrap gap-2 p-3 border-b dark:border-charcoal-blue-800 bg-gray-50 dark:bg-charcoal-blue-900">
				{quickActions.map((action, index) => (
					<button
						key={index}
						onClick={() => setInput(action.prompt)}
						className="px-3 py-1 text-xs bg-white dark:bg-charcoal-blue-900 border border-gray-200 dark:border-charcoal-blue-800 rounded-full hover:bg-gray-100 dark:hover:bg-charcoal-blue-800 transition-colors"
					>
						{action.label}
					</button>
				))}
			</div>

			{/* Messages */}
			<div className="flex-1 overflow-y-auto p-4 space-y-4">
				{messages.map((message) => (
					<div
						key={message.id}
						className={`flex ${
							message.role === "user" ? "justify-end" : "justify-start"
						}`}
					>
						<div
							className={`max-w-[80%] rounded-lg px-4 py-3 ${
								message.role === "user"
									? "bg-green-600 text-white rounded-br-none"
									: "bg-gray-100 dark:bg-charcoal-blue-900 text-gray-800 dark:text-gray-200 rounded-bl-none"
							}`}
						>
							<p className="whitespace-pre-wrap wrap-break-word">{message.content}</p>
							<p
								className={`text-xs mt-1 ${
									message.role === "user" ? "text-green-200" : "text-gray-500 dark:text-gray-400"
								}`}
							>
								{message.timestamp.toLocaleTimeString([], {
									hour: "2-digit",
									minute: "2-digit",
								})}
							</p>
						</div>
					</div>
				))}

				{isLoading && (
					<div className="flex justify-start">
						<div className="bg-gray-100 dark:bg-charcoal-blue-900 rounded-lg px-4 py-3 rounded-bl-none">
							<div className="flex space-x-1">
								<div className="w-2 h-2 bg-gray-400 dark:bg-gray-500 rounded-sm animate-bounce"></div>
								<div
									className="w-2 h-2 bg-gray-400 dark:bg-gray-500 rounded-sm animate-bounce"
									style={{ animationDelay: "0.1s" }}
								></div>
								<div
									className="w-2 h-2 bg-gray-400 dark:bg-gray-500 rounded-sm animate-bounce"
									style={{ animationDelay: "0.2s" }}
								></div>
							</div>
						</div>
					</div>
				)}

				<div ref={messagesEndRef} />
			</div>

			{/* Input */}
			<div className="p-4 border-t dark:border-charcoal-blue-800">
				<div className="flex items-end space-x-2">
					<textarea
						value={input}
						onChange={(e) => setInput(e.target.value)}
						onKeyPress={handleKeyPress}
						placeholder="Ask about nutrition, log food, or get recipe ideas..."
						className="flex-1 px-4 py-2 border dark:border-charcoal-blue-800 rounded-lg bg-white dark:bg-charcoal-blue-900 dark:text-charcoal-blue-100 focus:outline-none focus:ring-2 focus:ring-green-500 resize-none"
						rows={1}
						disabled={isLoading}
					/>
					<button
						onClick={handleSend}
						disabled={!input.trim() || isLoading}
						className="p-2 bg-green-600 text-white rounded-lg hover:bg-green-700 disabled:opacity-50 disabled:cursor-not-allowed"
					>
						<i className="ri-send-plane-fill text-lg"></i>
					</button>
				</div>
				<p className="text-xs text-gray-400 dark:text-gray-500 mt-2 text-center">
					Powered by GPT-4o with Semantic Kernel
				</p>
			</div>
		</div>
	);
}
