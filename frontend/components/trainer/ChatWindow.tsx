"use client";

import { useEffect, useState, useRef, useCallback } from "react";
import {
	connectToChat,
	sendMessage,
	joinConversation,
	leaveConversation,
	onReceiveMessage,
	sendTypingIndicator,
	onUserTyping,
	ChatMessage,
} from "@/lib/signalr";
import { formatDistanceToNow } from "date-fns";

interface ChatWindowProps {
	conversationId: string;
	currentUserId: string;
	recipientName: string;
}

export function ChatWindow({
	conversationId,
	currentUserId,
	recipientName,
}: ChatWindowProps) {
	const [messages, setMessages] = useState<ChatMessage[]>([]);
	const [newMessage, setNewMessage] = useState("");
	const [isConnected, setIsConnected] = useState(false);
	const [isTyping, setIsTyping] = useState(false);
	const [otherUserTyping, setOtherUserTyping] = useState(false);
	const messagesEndRef = useRef<HTMLDivElement>(null);
	const typingTimeoutRef = useRef<NodeJS.Timeout | undefined>(undefined);

	const scrollToBottom = () => {
		messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
	};

	useEffect(() => {
		scrollToBottom();
	}, [messages]);

	useEffect(() => {
		let mounted = true;

		async function setupConnection() {
			try {
				await connectToChat();
				await joinConversation(conversationId);

				if (mounted) {
					setIsConnected(true);
				}

				onReceiveMessage((message) => {
					if (mounted) {
						setMessages((prev) => [...prev, message]);
					}
				});

				onUserTyping(({ userId, isTyping }) => {
					if (mounted && userId !== currentUserId) {
						setOtherUserTyping(isTyping);
					}
				});
			} catch (error) {
				console.error("Failed to connect to chat:", error);
			}
		}

		setupConnection();

		return () => {
			mounted = false;
			leaveConversation(conversationId).catch(console.error);
		};
	}, [conversationId, currentUserId]);

	const handleTyping = useCallback(() => {
		if (!isTyping) {
			setIsTyping(true);
			sendTypingIndicator(conversationId, true);
		}

		if (typingTimeoutRef.current) {
			clearTimeout(typingTimeoutRef.current);
		}

		typingTimeoutRef.current = setTimeout(() => {
			setIsTyping(false);
			sendTypingIndicator(conversationId, false);
		}, 2000);
	}, [conversationId, isTyping]);

	const handleSend = async () => {
		if (!newMessage.trim() || !isConnected) return;

		try {
			await sendMessage(conversationId, newMessage.trim());
			setNewMessage("");

			if (typingTimeoutRef.current) {
				clearTimeout(typingTimeoutRef.current);
			}
			setIsTyping(false);
			sendTypingIndicator(conversationId, false);
		} catch (error) {
			console.error("Failed to send message:", error);
		}
	};

	const handleKeyPress = (e: React.KeyboardEvent) => {
		if (e.key === "Enter" && !e.shiftKey) {
			e.preventDefault();
			handleSend();
		}
	};

	return (
		<div className="flex flex-col h-150 bg-white dark:bg-slate-900 rounded-lg shadow-md">
			{/* Header */}
			<div className="flex items-center justify-between p-4 border-b dark:border-slate-700">
				<div className="flex items-center space-x-3">
					<div className="w-10 h-10 bg-green-100 dark:bg-green-900/30 rounded-full flex items-center justify-center">
						<span className="text-green-600 dark:text-green-400 font-semibold">
							{recipientName.charAt(0)}
						</span>
					</div>
					<div>
						<p className="font-medium">{recipientName}</p>
						<p className="text-xs text-gray-500 dark:text-gray-400">
							{isConnected ? (
								<span className="text-green-600 dark:text-green-400">● Online</span>
							) : (
								<span className="text-gray-400 dark:text-gray-500">Connecting...</span>
							)}
						</p>
					</div>
				</div>
			</div>

			{/* Messages */}
			<div className="flex-1 overflow-y-auto p-4 space-y-4">
				{messages.map((message) => {
					const isOwn = message.senderId === currentUserId;
					return (
						<div
							key={message.id}
							className={`flex ${isOwn ? "justify-end" : "justify-start"}`}
						>
							<div
								className={`max-w-[70%] rounded-lg px-4 py-2 ${
									isOwn
										? "bg-green-600 text-white rounded-br-none"
										: "bg-gray-100 dark:bg-slate-800 text-gray-800 dark:text-gray-200 rounded-bl-none"
								}`}
							>
								<p className="wrap-break-word">{message.content}</p>
								<p
									className={`text-xs mt-1 ${
										isOwn ? "text-green-200" : "text-gray-500 dark:text-gray-400"
									}`}
								>
									{formatDistanceToNow(new Date(message.sentAt), {
										addSuffix: true,
									})}
								</p>
							</div>
						</div>
					);
				})}

				{otherUserTyping && (
					<div className="flex justify-start">
						<div className="bg-gray-100 dark:bg-slate-800 rounded-lg px-4 py-2 rounded-bl-none">
							<div className="flex space-x-1">
								<div className="w-2 h-2 bg-gray-400 dark:bg-gray-500 rounded-full animate-bounce"></div>
								<div
									className="w-2 h-2 bg-gray-400 dark:bg-gray-500 rounded-full animate-bounce"
									style={{ animationDelay: "0.1s" }}
								></div>
								<div
									className="w-2 h-2 bg-gray-400 dark:bg-gray-500 rounded-full animate-bounce"
									style={{ animationDelay: "0.2s" }}
								></div>
							</div>
						</div>
					</div>
				)}

				<div ref={messagesEndRef} />
			</div>

			{/* Input */}
			<div className="p-4 border-t dark:border-slate-700">
				<div className="flex items-center space-x-2">
					<input
						type="text"
						value={newMessage}
						onChange={(e) => {
							setNewMessage(e.target.value);
							handleTyping();
						}}
						onKeyPress={handleKeyPress}
						placeholder="Type a message..."
						className="flex-1 px-4 py-2 border dark:border-slate-700 rounded-full bg-white dark:bg-slate-800 dark:text-slate-100 focus:outline-none focus:ring-2 focus:ring-green-500"
						disabled={!isConnected}
					/>
					<button
						onClick={handleSend}
						disabled={!newMessage.trim() || !isConnected}
						className="p-2 bg-green-600 text-white rounded-full hover:bg-green-700 disabled:opacity-50 disabled:cursor-not-allowed"
					>
						<i className="ri-send-plane-fill text-lg"></i>
					</button>
				</div>
			</div>
		</div>
	);
}
