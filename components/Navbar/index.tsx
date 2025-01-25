
import Link from "next/link";
import logoTransparent from "@/public/logo_transparent.png";
import Image from "next/image";
import { getUserOptionalServer } from "@/helper/session";
import NavbarContent from "./NavbarContent";

export default async function Navbar() {
	const user = await getUserOptionalServer();

	return (
		<nav className="bg-emerald-700 p-4">
			<div className="flex justify-between items-center">
				<Link href="/" className="flex flex-row items-center gap-2">
					<Image src={logoTransparent} alt="MacroChef" width={50} height={50} />
					<span className="text-white text-2xl font-bold">MacroChef</span>
				</Link>
				<NavbarContent user={user} />
			</div>
		</nav>
	)
}
