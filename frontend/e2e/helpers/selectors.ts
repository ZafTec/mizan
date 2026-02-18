export const tid = (id: string) => `[data-testid="${id}"]`;

export const SELECTORS = {
	// Nav
	navbar: "navbar",
	navMobileToggle: "nav-mobile-toggle",
	navMobileMenu: "nav-mobile-menu",
	navUserMenu: "nav-user-menu",

	// Landing
	heroSection: "hero-section",
	featureSection: "feature-section",
	testimonialSection: "testimonial-section",
	ctaSection: "cta-section",

	// Auth
	loginForm: "login-form",
	loginEmail: "login-email",
	loginPassword: "login-password",
	loginSubmit: "login-submit",
	registerForm: "register-form",
	registerEmail: "register-email",
	registerPassword: "register-password",
	registerConfirmPassword: "register-confirm-password",
	registerSubmit: "register-submit",
	forgotPasswordForm: "forgot-password-form",
	resetPasswordForm: "reset-password-form",
	passwordToggle: "password-toggle",

	// Dashboard
	dashboardStats: "dashboard-stats",
	recipeList: "recipe-list",
	recipeCard: "recipe-card",
	mealList: "meal-list",
	ingredientList: "ingredient-list",
	searchInput: "search-input",

	// Shared
	spinner: "spinner",
	errorMessage: "error-message",
} as const;
