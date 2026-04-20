import Link from "next/link";
import logoTransparent from "@/public/logo_transparent.png";
import Image from "next/image";
import { getUserOptionalServer } from "@/helper/session";
import NavbarContent from "./NavbarContent";

export default async function Navbar() {
	const user = await getUserOptionalServer();

	return (
		<nav className="sticky top-0 z-50 px-3 pt-3 sm:px-4 lg:px-6">
			<div className="max-w-7xl mx-auto">
				<div className="nav-glass relative flex h-16 items-center justify-between rounded-[30px] px-4 sm:px-6">
					<Link href="/" className="group flex items-center gap-3">
						<div className="relative">
							<div className="absolute inset-0 rounded-2xl bg-brand-500/10 blur-lg opacity-25 transition-opacity group-hover:opacity-40" />
							<Image
								src={logoTransparent}
								alt="Mizan"
								width={42}
								height={42}
								className="relative rounded-2xl"
							/>
						</div>
					<div className="flex flex-col">
						<span className="text-xl font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
							Mizan
						</span>
						<span className="hidden items-center gap-1 text-[10px] uppercase tracking-[0.18em] text-charcoal-blue-500 dark:text-charcoal-blue-400 sm:inline-flex">
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
