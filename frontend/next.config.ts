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
