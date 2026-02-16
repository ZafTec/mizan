"use client";

import { useEffect, useState } from "react";
import { clientApi } from "@/lib/api.client";
import Link from "next/link";
import { formatDistanceToNow } from "date-fns";

interface Message {
	id: string;
	conversationId: string;
	senderId: string;
	senderName: string;
	senderImage?: string;
	content: string;
	sentAt: string;
	isRead: boolean;
}

export function RecentMessages() {
	const [messages, setMessages] = useState<Message[]>([]);
	const [loading, setLoading] = useState(true);

	useEffect(() => {
		async function fetchMessages() {
			try {
				const data = await clientApi<{ messages: Message[] }>(
					"/api/trainer/messages/recent"
				);
				setMessages(data.messages);
			} catch (error) {
				console.error("Failed to fetch messages:", error);
				// Demo data
				setMessages([
					{
						id: "1",
						conversationId: "conv-1",
						senderId: "user-1",
						senderName: "Abebe Kebede",
						content: "Hey coach, I hit my protein goal today! 💪",
						sentAt: new Date(Date.now() - 1000 * 60 * 30).toISOString(),
						isRead: false,
					},
					{
						id: "2",
						conversationId: "conv-2",
						senderId: "user-2",
						senderName: "Tigist Haile",
						content:
							"Can we adjust my calorie target? I feel like it might be too low.",
						sentAt: new Date(Date.now() - 1000 * 60 * 60 * 2).toISOString(),
						isRead: true,
					},
					{
						id: "3",
						conversationId: "conv-3",
						senderId: "user-3",
						senderName: "Dawit Yohannes",
						content: "Thanks for the workout plan! Starting tomorrow.",
						sentAt: new Date(Date.now() - 1000 * 60 * 60 * 5).toISOString(),
						isRead: true,
					},
				]);
			} finally {
				setLoading(false);
			}
		}

		fetchMessages();
	}, []);

	if (loading) {
		return (
			<div className="space-y-4">
				{[1, 2, 3].map((i) => (
					<div key={i} className="animate-pulse">
						<div className="flex items-start space-x-3">
							<div className="w-10 h-10 bg-gray-200 dark:bg-slate-700 rounded-full"></div>
							<div className="flex-1">
								<div className="h-4 bg-gray-200 dark:bg-slate-700 rounded w-1/4 mb-2"></div>
								<div className="h-3 bg-gray-200 dark:bg-slate-700 rounded w-3/4"></div>
							</div>
						</div>
					</div>
				))}
			</div>
		);
	}

	return (
		<div className="space-y-4">
			{messages.map((message) => (
				<Link
					key={message.id}
					href={`/trainer/chat/${message.conversationId}`}
					className={`block p-3 rounded-lg transition-colors ${
						message.isRead
							? "bg-gray-50 dark:bg-slate-800 hover:bg-gray-100 dark:hover:bg-slate-700"
							: "bg-blue-50 dark:bg-blue-950 hover:bg-blue-100 dark:hover:bg-blue-900/30"
					}`}
				>
					<div className="flex items-start space-x-3">
						<div className="w-10 h-10 bg-green-100 dark:bg-green-900/30 rounded-full flex items-center justify-center shrink-0">
							{message.senderImage ? (
								<img
									src={message.senderImage}
									alt={message.senderName}
									className="w-10 h-10 rounded-full"
								/>
							) : (
								<span className="text-green-600 dark:text-green-400 font-semibold">
									{message.senderName.charAt(0)}
								</span>
							)}
						</div>
						<div className="flex-1 min-w-0">
							<div className="flex items-center justify-between">
								<p className="font-medium text-sm">{message.senderName}</p>
								<span className="text-xs text-gray-500 dark:text-gray-400">
									{formatDistanceToNow(new Date(message.sentAt), {
										addSuffix: true,
									})}
								</span>
							</div>
							<p className="text-sm text-gray-600 dark:text-gray-400 truncate">{message.content}</p>
						</div>
						{!message.isRead && (
							<div className="w-2 h-2 bg-blue-600 rounded-full shrink-0"></div>
						)}
					</div>
				</Link>
			))}

			{messages.length === 0 && (
				<p className="text-center text-gray-500 dark:text-gray-400 py-8">No recent messages.</p>
			)}

			<Link
				href="/trainer/chat"
				className="block text-center text-green-600 dark:text-green-400 hover:text-green-700 dark:hover:text-green-300 text-sm font-medium pt-2"
			>
				View All Messages →
			</Link>
		</div>
	);
}
