import Link from "next/link";
import logoTransparent from "@/public/logo_transparent.png";
import Image from "next/image";
import { getUserOptionalServer } from "@/helper/session";
import NavbarContent from "./NavbarContent";

export default async function Navbar() {
	const user = await getUserOptionalServer();

	return (
		<nav className="sticky top-0 z-50 backdrop-blur-xl bg-white/80 border-b border-slate-200/50 shadow-sm">
			<div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
				<div className="flex justify-between items-center h-16">
					<Link href="/" className="flex items-center gap-3 group">
						<div className="relative">
							<div className="absolute inset-0 bg-gradient-to-r from-brand-400 to-brand-600 rounded-xl blur-lg opacity-30 group-hover:opacity-50 transition-opacity" />
							<Image
								src={logoTransparent}
								alt="Mizan"
								width={42}
								height={42}
								className="relative rounded-xl"
							/>
						</div>
						<div className="flex flex-col">
							<span className="text-xl font-bold bg-gradient-to-r from-brand-600 to-brand-700 bg-clip-text text-transparent">
								Mizan
							</span>
							<span className="text-[10px] text-slate-500 -mt-1 hidden sm:block">
								ሚዛን • Balance
							</span>
						</div>
					</Link>
					<NavbarContent user={user} />
				</div>
			</div>
		</nav>
	);
}
