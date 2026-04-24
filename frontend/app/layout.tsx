import type { Metadata } from "next";
import Image from "next/image";
import { cookies } from "next/headers";
import "./globals.css";
import Navbar from "@/components/Navbar";
import { AnimatedIcon } from "@/components/ui/animated-icon";
import { Toaster } from "@/components/ui/sonner";
import { AppearanceSync } from "@/components/appearance/AppearanceSync";
import { getUserOptionalServer } from "@/helper/session";
import { getAppearanceSettingsFromUser, getServerAppearanceClasses } from "@/lib/appearance";
import { APPEARANCE_COOKIE, parseAppearanceCookie } from "@/lib/appearance-cookie";
import { APPEARANCE_SCRIPT } from "@/lib/appearance-script";
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
	const [user, cookieStore] = await Promise.all([userPromise, cookies()]);

	// Cookie wins over user record: cookie is the latest local intent and the client
	// has already applied it via the pre-hydration script. Falling back to the user
	// record covers fresh devices where the cookie isn't set yet.
	const cookieAppearance = parseAppearanceCookie(cookieStore.get(APPEARANCE_COOKIE)?.value);
	const effectiveAppearance = cookieAppearance ?? getAppearanceSettingsFromUser(user);
	const htmlClasses = getServerAppearanceClasses(effectiveAppearance);

	return (
		<html lang="en" className={htmlClasses.join(" ")} suppressHydrationWarning>
			<head>
				{/* Runs synchronously before hydration so "system" preference + prefers-color-scheme
					apply to the first paint. No flash. */}
				<script dangerouslySetInnerHTML={{ __html: APPEARANCE_SCRIPT }} />
			</head>
			<body className="min-h-screen antialiased flex flex-col selection:bg-brand-500/15 selection:text-charcoal-blue-900 dark:selection:text-charcoal-blue-50">
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
									<Image src={logoTransparent} alt="Mizan" fill sizes="44px" className="object-cover" priority />
								</div>
								<div>
									<p className="text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
										Mizan
									</p>
									<p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">ሚዛን • Balanced nutrition, training, and coaching.</p>
								</div>
							</div>

							<div className="flex flex-col items-center justify-center gap-2 text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400 sm:flex-row sm:flex-wrap sm:gap-x-4 sm:gap-y-2">
								<p>
									&copy; {new Date().getFullYear()} Mizan, a{" "}
									<a
										href="https://zaftech.co"
										target="_blank"
										rel="noopener noreferrer"
										className="footer-link font-medium"
									>
										Zaftech
									</a>
									{" "}product
								</p>
								<span className="hidden text-charcoal-blue-300 dark:text-charcoal-blue-600 sm:inline">|</span>
								<a href="https://zaftech.co/privacy" target="_blank" rel="noopener noreferrer" className="footer-link">
									Privacy
								</a>
								<span className="hidden text-charcoal-blue-300 dark:text-charcoal-blue-600 sm:inline">|</span>
								<a href="https://zaftech.co/terms" target="_blank" rel="noopener noreferrer" className="footer-link">
									Terms
								</a>
								<span className="hidden text-charcoal-blue-300 dark:text-charcoal-blue-600 sm:inline">|</span>
								<a href="https://zaftech.co/refunds" target="_blank" rel="noopener noreferrer" className="footer-link">
									Refunds
								</a>
							</div>

							<div className="flex items-center justify-center gap-3 sm:justify-end">
								<a
									href="https://www.youtube.com/@Zaftec"
									target="_blank"
									rel="noopener noreferrer"
									className="icon-chip h-11 w-11 text-charcoal-blue-500 hover:text-charcoal-blue-900 dark:text-charcoal-blue-400 dark:hover:text-white"
									aria-label="Zaftech on YouTube"
								>
									<i className="ri-youtube-fill text-lg" aria-hidden="true" />
								</a>
								<a
									href="https://x.com/ZaftechS"
									target="_blank"
									rel="noopener noreferrer"
									className="icon-chip h-11 w-11 text-charcoal-blue-500 hover:text-charcoal-blue-900 dark:text-charcoal-blue-400 dark:hover:text-white"
									aria-label="Zaftech on X"
								>
									<AnimatedIcon name="twitter" size={18} aria-hidden="true" />
								</a>
								<a
									href="https://zaftech.co"
									target="_blank"
									rel="noopener noreferrer"
									className="icon-chip h-11 w-11 text-charcoal-blue-500 hover:text-charcoal-blue-900 dark:text-charcoal-blue-400 dark:hover:text-white"
									aria-label="Zaftech website"
								>
									<i className="ri-global-line text-lg" aria-hidden="true" />
								</a>
							</div>
						</div>
					</div>
				</footer>
			</body>
		</html>
	);
}
