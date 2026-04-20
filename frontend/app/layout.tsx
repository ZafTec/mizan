import type { Metadata } from "next";
import Image from "next/image";
import "./globals.css";
import Navbar from "@/components/Navbar";
import { AnimatedIcon } from "@/components/ui/animated-icon";
import { Toaster } from "@/components/ui/sonner";
import { AppearanceSync } from "@/components/appearance/AppearanceSync";
import { getUserOptionalServer } from "@/helper/session";
import { getAppearanceSettingsFromUser, getServerAppearanceClasses } from "@/lib/appearance";
import logoTransparent from "@/public/logo_transparent.png";
import 'remixicon/fonts/remixicon.css';

export const dynamic = 'force-dynamic';

export const metadata: Metadata = {
	title: "Mizan - Balanced Nutrition & Fitness",
	description: "Your personal nutrition and fitness companion. Track meals, plan diets, and achieve your health goals with AI-powered coaching.",
};

export default function RootLayout(
	{ children }: Readonly<{ children: React.ReactNode }>
) {
	const userPromise = getUserOptionalServer();

	return <LayoutContent userPromise={userPromise}>{children}</LayoutContent>;
}

async function LayoutContent({
	children,
	userPromise,
}: Readonly<{ children: React.ReactNode; userPromise: ReturnType<typeof getUserOptionalServer> }>) {
	const user = await userPromise;
	const htmlClasses = getServerAppearanceClasses(getAppearanceSettingsFromUser(user));

	return (
		<html lang="en" className={htmlClasses.join(" ")}>
			<body className="min-h-screen antialiased flex flex-col selection:bg-brand-500/15 selection:text-slate-950 dark:selection:text-white">
				<AppearanceSync />
				<Toaster position="top-right" />
				<Navbar />
				<main className="grow pb-8">
					<div className="page-transition max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-6 sm:py-8 lg:py-10">
						{children}
					</div>
				</main>

				<footer className="px-4 pb-4 sm:px-6 lg:px-8 lg:pb-6">
					<div className="surface-panel max-w-7xl mx-auto px-5 py-6 sm:px-6 sm:py-7">
						<div className="grid grid-cols-1 gap-6 text-center sm:grid-cols-3 sm:text-left">
							<div className="flex items-center justify-center gap-3 sm:justify-start">
								<div className="relative h-11 w-11 overflow-hidden rounded-2xl ring-1 ring-brand-500/20">
									<Image src={logoTransparent} alt="Mizan" fill className="object-cover" priority />
								</div>
								<div>
									<p className="text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
										Mizan
									</p>
									<p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">ሚዛን • Balanced nutrition, training, and coaching.</p>
								</div>
							</div>

							<div className="flex flex-col items-center justify-center gap-2 sm:flex-row sm:gap-4">
								<p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
									&copy; {new Date().getFullYear()} Mizan
								</p>
								<span className="hidden text-slate-300 dark:text-slate-600 sm:inline">|</span>
								<a href="/privacy" className="footer-link">
									Privacy
								</a>
								<span className="hidden text-slate-300 dark:text-slate-600 sm:inline">|</span>
								<a href="/terms" className="footer-link">
									Terms
								</a>
							</div>

							<div className="flex items-center justify-center gap-3 sm:justify-end">
								<a href="#" className="icon-chip h-11 w-11 text-slate-500 hover:text-slate-900 dark:text-slate-400 dark:hover:text-white" aria-label="GitHub">
									<AnimatedIcon name="github" size={18} aria-hidden="true" />
								</a>
								<a href="#" className="icon-chip h-11 w-11 text-slate-500 hover:text-slate-900 dark:text-slate-400 dark:hover:text-white" aria-label="Twitter">
									<AnimatedIcon name="twitter" size={18} aria-hidden="true" />
								</a>
							</div>
						</div>
					</div>
				</footer>
			</body>
		</html>
	);
}
