import type { NextConfig } from "next";

const nextConfig: NextConfig = {
	// Enable standalone output for Docker
	output: "standalone",

	// Allow the dev server to serve HMR/dev resources when the app is accessed
	// through a tunnel (e.g. ngrok, for local Paddle checkout domain testing).
	// Without this, Next.js blocks cross-origin dev requests and pages silently
	// fail to hydrate client-side handlers (forms fall back to native GET submit).
	allowedDevOrigins: process.env.ALLOWED_DEV_ORIGINS?.split(",") ?? [],

	// bun is a runtime-only module: tell Next.js not to bundle it
	serverExternalPackages: ["bun"],

	// Image configuration
	images: {
		remotePatterns: [
			{
				hostname: "res.cloudinary.com",
			},
			{
				hostname: "lh3.googleusercontent.com", // Google OAuth avatars
			},
			{
				hostname: "avatars.githubusercontent.com", // GitHub avatars
			},
		],
	},

	// Headers for security
	async headers() {
		return [
			{
				source: "/(.*)",
				headers: [
					{
						key: "X-Frame-Options",
						value: "DENY",
					},
					{
						key: "X-Content-Type-Options",
						value: "nosniff",
					},
					{
						key: "Referrer-Policy",
						value: "strict-origin-when-cross-origin",
					},
				],
			},
		];
	},
};

export default nextConfig;
