import type { NextConfig } from "next";
// import { WithPWA } from 'next-pwa';

// const withPWA = require('next-pwa')({
// 	dest: 'public',
// 	register: true,
// 	skipWaiting: true,
// 	disable: process.env.NODE_ENV === 'development'
// })

const nextConfig: NextConfig = {
	/* config options here */
	images: {
		remotePatterns: [
			{
				hostname: 'res.cloudinary.com'
			}
		],
	}
};

export default nextConfig;
