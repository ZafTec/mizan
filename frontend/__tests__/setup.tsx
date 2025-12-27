import "@testing-library/jest-dom";
import { vi } from "vitest";

// Mock next/navigation
vi.mock("next/navigation", () => ({
	useRouter: () => ({
		push: vi.fn(),
		replace: vi.fn(),
		prefetch: vi.fn(),
		back: vi.fn(),
	}),
	usePathname: () => "/",
	useSearchParams: () => new URLSearchParams(),
}));

// Mock next/image
vi.mock("next/image", () => ({
	default: ({ src, alt, ...props }: { src: string; alt: string }) => {
		// eslint-disable-next-line @next/next/no-img-element
		return <img src={src} alt={alt} {...props} />;
	},
}));

// Mock better-auth client
vi.mock("@/lib/auth-client", () => ({
	authClient: {
		signIn: vi.fn(),
		signUp: vi.fn(),
		signOut: vi.fn(),
		getSession: vi.fn(),
		token: vi.fn().mockResolvedValue({ data: { token: "mock-token" } }),
	},
	useSession: vi.fn().mockReturnValue({
		data: null,
		isPending: false,
		error: null,
	}),
	signIn: vi.fn(),
	signUp: vi.fn(),
	signOut: vi.fn(),
	getSession: vi.fn(),
	getApiToken: vi.fn().mockResolvedValue("mock-token"),
	apiClient: vi.fn(),
}));

// Mock SignalR
vi.mock("@microsoft/signalr", () => ({
	HubConnectionBuilder: vi.fn().mockImplementation(() => ({
		withUrl: vi.fn().mockReturnThis(),
		withAutomaticReconnect: vi.fn().mockReturnThis(),
		configureLogging: vi.fn().mockReturnThis(),
		build: vi.fn().mockReturnValue({
			start: vi.fn().mockResolvedValue(undefined),
			stop: vi.fn().mockResolvedValue(undefined),
			on: vi.fn(),
			invoke: vi.fn(),
			state: "Connected",
		}),
	})),
	HubConnectionState: {
		Connected: "Connected",
		Disconnected: "Disconnected",
	},
	LogLevel: {
		Information: 1,
	},
}));

// Global fetch mock
global.fetch = vi.fn();

// Suppress console errors in tests
const originalError = console.error;
beforeAll(() => {
	console.error = (...args: unknown[]) => {
		if (
			typeof args[0] === "string" &&
			args[0].includes("Warning: ReactDOM.render")
		) {
			return;
		}
		originalError.call(console, ...args);
	};
});

afterAll(() => {
	console.error = originalError;
});
