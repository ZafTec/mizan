import { clientApi } from "@/lib/api.client";

export interface TelegramLink {
	linked: boolean;
	telegramUsername: string | null;
	linkedAt: string | null;
	lastSeenAt: string | null;
	botUsername: string | null;
	botConfigured: boolean;
}

export interface TelegramLinkCode {
	code: string;
	deepLink: string;
	expiresAt: string;
}

export const getTelegramLink = () => clientApi<TelegramLink>("/api/Telegram/link");

export const issueTelegramCode = () =>
	clientApi<TelegramLinkCode>("/api/Telegram/link", { method: "POST" });

export const unlinkTelegram = () =>
	clientApi<void>("/api/Telegram/link", { method: "DELETE" });
