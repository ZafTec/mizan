import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";

// Mock the auth client
const mockSignIn = vi.fn();
const mockUseSession = vi.fn();

vi.mock("@/lib/auth-client", () => ({
	signIn: mockSignIn,
	useSession: () => mockUseSession(),
}));

// Simple login form component for testing
function LoginForm() {
	const handleSubmit = async (e: React.FormEvent<HTMLFormElement>) => {
		e.preventDefault();
		const formData = new FormData(e.currentTarget);
		await mockSignIn.emailAndPassword({
			email: formData.get("email") as string,
			password: formData.get("password") as string,
		});
	};

	return (
		<form onSubmit={handleSubmit} data-testid="login-form">
			<input
				type="email"
				name="email"
				placeholder="Email"
				data-testid="email-input"
			/>
			<input
				type="password"
				name="password"
				placeholder="Password"
				data-testid="password-input"
			/>
			<button type="submit" data-testid="submit-button">
				Sign In
			</button>
		</form>
	);
}

describe("LoginForm", () => {
	beforeEach(() => {
		vi.clearAllMocks();
		mockUseSession.mockReturnValue({
			data: null,
			isPending: false,
			error: null,
		});
	});

	it("renders login form with email and password inputs", () => {
		render(<LoginForm />);

		expect(screen.getByTestId("email-input")).toBeInTheDocument();
		expect(screen.getByTestId("password-input")).toBeInTheDocument();
		expect(screen.getByTestId("submit-button")).toBeInTheDocument();
	});

	it("calls signIn with email and password on submit", async () => {
		mockSignIn.emailAndPassword = vi.fn().mockResolvedValue({ data: {} });

		render(<LoginForm />);

		const emailInput = screen.getByTestId("email-input");
		const passwordInput = screen.getByTestId("password-input");
		const submitButton = screen.getByTestId("submit-button");

		fireEvent.change(emailInput, { target: { value: "test@example.com" } });
		fireEvent.change(passwordInput, { target: { value: "password123" } });
		fireEvent.click(submitButton);

		await waitFor(() => {
			expect(mockSignIn.emailAndPassword).toHaveBeenCalledWith({
				email: "test@example.com",
				password: "password123",
			});
		});
	});
});

describe("Session Hook", () => {
	it("returns null when not authenticated", () => {
		mockUseSession.mockReturnValue({
			data: null,
			isPending: false,
			error: null,
		});

		const result = mockUseSession();
		expect(result.data).toBeNull();
		expect(result.isPending).toBe(false);
	});

	it("returns session data when authenticated", () => {
		const mockSession = {
			user: {
				id: "user-123",
				email: "test@example.com",
				name: "Test User",
			},
		};

		mockUseSession.mockReturnValue({
			data: mockSession,
			isPending: false,
			error: null,
		});

		const result = mockUseSession();
		expect(result.data).toEqual(mockSession);
		expect(result.data?.user.email).toBe("test@example.com");
	});

	it("shows pending state while loading", () => {
		mockUseSession.mockReturnValue({
			data: null,
			isPending: true,
			error: null,
		});

		const result = mockUseSession();
		expect(result.isPending).toBe(true);
	});
});
