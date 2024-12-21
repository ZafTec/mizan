import type { Metadata } from "next";
import "./globals.css";
import Navbar from "@/components/Navbar";
import 'remixicon/fonts/remixicon.css';

export const metadata: Metadata = {
	title: "MacroChef - Recipe Manager",
	description: "A simple recipe manager to help you track your macros.",
};

export default function RootLayout(
	{ children }: Readonly<{ children: React.ReactNode }>
) {
	return (
		<html lang="en">
			<body
				className={`antialiased h-screen bg-gray-100 flex flex-col`}
			>
				{/* navbar */}
				<Navbar />
				<main className="p-8 flex-1 max-w-full overflow-x-auto">
					{children}
				</main>

				<footer className="bg-gray-800 text-white text-center py-4">
					<div className="container mx-auto">
						<p>&copy; {new Date().getFullYear()} MacroChef</p>
					</div>
				</footer>
			</body>
		</html>
	)
}
