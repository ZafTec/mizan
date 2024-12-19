
import Link from "next/link";
import { auth } from "@/context/auth";

export default async function Navbar() {
	const session = await auth();
	return (
		<nav className="bg-gray-800 p-4">
			<div className="container mx-auto flex justify-between items-center">
				<Link href="/" className="text-white text-2xl font-bold">MacroChef</Link>
				<div>
					<Link href="/about" className="text-white">About</Link>
					<Link href="/ingredients" className="text-white ml-4">Ingredients</Link>
					{session ? (
						<>
							<Link href="/recipes" className="text-white ml-4">Recipes</Link>
							<Link href="/api/auth/signout?callbackUrl=/" className="text-white ml-4">Sign Out</Link>
						</>
					) : (
						<Link href="/api/auth/signin" className="text-white ml-4">Sign In</Link>
					)}

				</div>
			</div>
		</nav>
	)
}
