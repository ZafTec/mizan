import type { Metadata } from "next";
import "./globals.css";
import Navbar from "@/components/Navbar";
import 'remixicon/fonts/remixicon.css';

export const dynamic = 'force-dynamic';

export const metadata: Metadata = {
	title: "Mizan - Balanced Nutrition & Fitness",
	description: "Your personal nutrition and fitness companion. Track meals, plan diets, and achieve your health goals with AI-powered coaching.",
};

export default function RootLayout(
	{ children }: Readonly<{ children: React.ReactNode }>
) {
	return (
		<html lang="en">
			<body className="antialiased min-h-screen flex flex-col">
				<Navbar />
				<main className="grow">
					<div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-6 sm:py-8">
						{children}
					</div>
				</main>

				<footer className="border-t border-slate-200 bg-white/50 backdrop-blur-sm">
					<div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
						<div className="grid grid-cols-1 sm:grid-cols-3 gap-6 items-center text-center sm:text-left">
							{/* Brand */}
							<div className="flex items-center justify-center sm:justify-start gap-2">
								<span className="text-lg font-bold bg-linear-to-r from-brand-600 to-brand-700 bg-clip-text text-transparent">
									Mizan
								</span>
								<span className="text-slate-400">•</span>
								<span className="text-sm text-slate-500">ሚዛን</span>
							</div>

							{/* Links */}
							<div className="flex flex-col sm:flex-row items-center justify-center gap-2 sm:gap-4">
								<p className="text-sm text-slate-500">
									&copy; {new Date().getFullYear()} Mizan
								</p>
								<span className="hidden sm:inline text-slate-300">|</span>
								<a href="/privacy" className="text-sm text-slate-600 hover:text-brand-600 transition-colors">
									Privacy
								</a>
								<span className="hidden sm:inline text-slate-300">|</span>
								<a href="/terms" className="text-sm text-slate-600 hover:text-brand-600 transition-colors">
									Terms
								</a>
							</div>

							{/* Social */}
							<div className="flex items-center justify-center sm:justify-end gap-4">
								<a href="#" className="text-slate-400 hover:text-slate-600 transition-colors" aria-label="GitHub">
									<i className="ri-github-fill text-xl" />
								</a>
								<a href="#" className="text-slate-400 hover:text-slate-600 transition-colors" aria-label="Twitter">
									<i className="ri-twitter-x-fill text-xl" />
								</a>
							</div>
						</div>
					</div>
				</footer>
			</body>
		</html>
	);
}
