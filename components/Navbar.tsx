
import Link from "next/link";
import { auth } from "@/context/auth";
import logoTransparent from "@/public/logo_transparent.png";
import Image from "next/image";

export default async function Navbar() {
	const session = await auth();
	return (
		<nav className="bg-emerald-700 p-4">
			<div className="container mx-auto flex justify-between items-center">
                <Link href="/" className="flex flex-row items-center gap-2">
                    <Image src={logoTransparent} alt="MacroChef" width={50} height={50} />
				    <span className="text-white text-2xl font-bold">MacroChef</span>
                </Link>
				<div>
					{/* <Link href="/about" className="text-white">About</Link> */}
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
