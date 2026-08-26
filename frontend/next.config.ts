import type { NextConfig } from "next";

function hostnameOf(url: string | undefined): string | undefined {
	if (!url) return undefined;
	try {
		return new URL(url).hostname;
	} catch {
		return undefined;
	}
}

const mediaHostname = hostnameOf(process.env.NEXT_PUBLIC_MEDIA_URL);

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

	// Image configuration. Uploaded media lives in our own object store, whose
	// public host is deployment-specific - MinIO behind a proxy, an R2 custom
	// domain, r2.dev - so it comes from the environment rather than being
	// hardcoded. See docs/REFOCUS.md §7.
	images: {
		remotePatterns: [
			...(mediaHostname ? [{ hostname: mediaHostname }] : []),
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
