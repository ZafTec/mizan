import type { NextConfig } from "next";

const nextConfig: NextConfig = {
	// Enable standalone output for Docker
	output: "standalone",

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

	// Environment variables exposed to browser
	env: {
		NEXT_PUBLIC_APP_URL: process.env.NEXT_PUBLIC_APP_URL,
		NEXT_PUBLIC_API_URL: process.env.NEXT_PUBLIC_API_URL,
		NEXT_PUBLIC_CLOUDINARY_CLOUD_NAME: process.env.NEXT_PUBLIC_CLOUDINARY_CLOUD_NAME,
		NEXT_PUBLIC_CLOUDINARY_API_KEY: process.env.NEXT_PUBLIC_CLOUDINARY_API_KEY,
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

	// BFF Architecture: All backend API calls go through /api/bff/*
	// No direct rewrites to backend - the BFF proxy route handles forwarding
};

export default nextConfig;
