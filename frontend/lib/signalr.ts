import * as signalR from "@microsoft/signalr";
import { getApiToken } from "./auth-client";
import { logger } from "./logger";

const chatLogger = logger.createModuleLogger("signalr-chat-service");

let connection: signalR.HubConnection | null = null;

export interface ChatMessage {
	id: string;
	senderId: string;
	senderName: string;
	content: string;
	messageType: string;
	sentAt: string;
}

export interface WorkoutProgress {
	workoutId: string;
	completedSets: number;
	totalSets: number;
	caloriesBurned?: number;
}

export async function connectToChat(): Promise<signalR.HubConnection> {
	if (connection?.state === signalR.HubConnectionState.Connected) {
		return connection;
	}

	const token = await getApiToken();
	const apiUrl = (typeof process !== 'undefined' && process.env["NEXT_PUBLIC_API_URL"]) || "http://localhost:5000";

	connection = new signalR.HubConnectionBuilder()
		.withUrl(`${apiUrl}/hubs/chat`, {
			accessTokenFactory: () => token || "",
		})
		.withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
		.configureLogging(signalR.LogLevel.Information)
		.build();

	try {
		await connection.start();
		chatLogger.info("Connected to SignalR chat hub");
	} catch (error) {
		chatLogger.error("Failed to connect to SignalR chat hub", { error });
		throw error;
	}

	return connection;
}

export function disconnectFromChat(): void {
	if (connection) {
		connection.stop();
		connection = null;
	}
}

export function onReceiveMessage(
	callback: (message: ChatMessage) => void
): void {
	connection?.on("ReceiveMessage", callback);
}

export function onNewMessageNotification(
	callback: (notification: {
		conversationId: string;
		senderId: string;
		senderName: string;
		preview: string;
	}) => void
): void {
	connection?.on("NewMessageNotification", callback);
}

export function onUserTyping(
	callback: (data: { userId: string; isTyping: boolean }) => void
): void {
	connection?.on("UserTyping", callback);
}

export function onWorkoutProgressUpdated(
	callback: (progress: WorkoutProgress) => void
): void {
	connection?.on("WorkoutProgressUpdated", callback);
}

export async function sendMessage(
	conversationId: string,
	content: string,
	messageType: string = "text"
): Promise<void> {
	if (!connection) {
		throw new Error("Not connected to chat");
	}

	await connection.invoke("SendMessage", {
		conversationId,
		content,
		messageType,
	});
}

export async function joinConversation(conversationId: string): Promise<void> {
	if (!connection) {
		throw new Error("Not connected to chat");
	}

	await connection.invoke("JoinConversation", conversationId);
}

export async function leaveConversation(conversationId: string): Promise<void> {
	if (!connection) {
		throw new Error("Not connected to chat");
	}

	await connection.invoke("LeaveConversation", conversationId);
}

export async function sendTypingIndicator(
	conversationId: string,
	isTyping: boolean
): Promise<void> {
	if (!connection) return;

	await connection.invoke("TypingIndicator", conversationId, isTyping);
}

export async function syncWorkoutProgress(
	progress: WorkoutProgress
): Promise<void> {
	if (!connection) {
		throw new Error("Not connected to chat");
	}

	await connection.invoke("SyncWorkoutProgress", progress);
}

// React hook for SignalR connection
export function useSignalR() {
	return {
		connect: connectToChat,
		disconnect: disconnectFromChat,
		onReceiveMessage,
		onNewMessageNotification,
		onUserTyping,
		onWorkoutProgressUpdated,
		sendMessage,
		joinConversation,
		leaveConversation,
		sendTypingIndicator,
		syncWorkoutProgress,
	};
}
