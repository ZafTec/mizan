import type { Metadata } from "next";
import "./globals.css";
import Navbar from "@/components/Navbar";
import 'remixicon/fonts/remixicon.css';

export const metadata: Metadata = {
	title: "MacroChef - Recipe Manager",
	description: "A simple recipe manager to help you track your macros.",
	viewport: "width=device-width, initial-scale=1.0",
};

export default function RootLayout(
	{ children }: Readonly<{ children: React.ReactNode }>
) {
	return (
		<html lang="en">
			<body
				className={`antialiased bg-gray-100 flex flex-col min-h-screen`}
			>
				{/* navbar */}
				<Navbar />
				<main className="p-3 sm:p-5 md:p-8 max-w-full overflow-x-hidden overflow-y-auto flex-grow">
					{children}
				</main>

				<footer className="bg-gray-800 text-white text-center py-4 mt-auto">
					<div className="container mx-auto">
						<p>&copy; {new Date().getFullYear()} MacroChef</p>
					</div>
				</footer>
			</body>
		</html>
	)
}
