"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { ChevronDown } from "lucide-react";
import { clientApi } from "@/lib/api.client";
import { appToast } from "@/lib/toast";
import { AnimatedIcon } from "@/components/ui/animated-icon";
import { cn } from "@/lib/utils";

type Household = { id: string; name: string; memberCount: number; isActive: boolean };
type MyHouseholds = { households: Household[]; activeHouseholdId?: string | null };

/**
 * Tier 2 - docs/REFOCUS.md §3. One household is the overwhelming case and gets
 * no chrome at all; the control appears only for people who actually have
 * somewhere to switch to.
 */
export default function HouseholdSwitcher() {
	const router = useRouter();
	const [households, setHouseholds] = useState<Household[]>([]);
	const [activeId, setActiveId] = useState<string | null>(null);
	const [open, setOpen] = useState(false);
	const [switching, setSwitching] = useState(false);
	const containerRef = useRef<HTMLDivElement>(null);

	useEffect(() => {
		let cancelled = false;
		clientApi<MyHouseholds>("/api/Households/mine")
			.then((data) => {
				if (cancelled) return;
				setHouseholds(data.households ?? []);
				setActiveId(
					data.activeHouseholdId ?? data.households?.find((h) => h.isActive)?.id ?? null,
				);
			})
			.catch(() => {
				// A switcher that cannot load its list simply does not appear.
			});
		return () => {
			cancelled = true;
		};
	}, []);

	useEffect(() => {
		if (!open) return;
		const onPointerDown = (event: MouseEvent) => {
			if (!containerRef.current?.contains(event.target as Node)) setOpen(false);
		};
		document.addEventListener("mousedown", onPointerDown);
		return () => document.removeEventListener("mousedown", onPointerDown);
	}, [open]);

	const select = useCallback(
		async (id: string) => {
			setOpen(false);
			if (id === activeId || switching) return;
			setSwitching(true);
			try {
				await clientApi("/api/Households/active", { method: "PUT", body: { householdId: id } });
				setActiveId(id);
				router.refresh();
			} catch (error) {
				appToast.error(error, "Could not switch household");
			} finally {
				setSwitching(false);
			}
		},
		[activeId, router, switching],
	);

	if (households.length < 2) return null;

	const active = households.find((h) => h.id === activeId) ?? households[0];

	return (
		<div ref={containerRef} className="relative">
			<button
				type="button"
				onClick={() => setOpen((o) => !o)}
				disabled={switching}
				aria-expanded={open}
				aria-haspopup="menu"
				className="flex items-center gap-2 rounded-2xl border border-charcoal-blue-200 bg-white px-3 py-1.5 text-sm text-charcoal-blue-700 transition-colors hover:border-charcoal-blue-300 disabled:opacity-60 dark:border-white/10 dark:bg-charcoal-blue-900 dark:text-charcoal-blue-200"
			>
				<AnimatedIcon name="home" size={14} />
				<span className="max-w-32 truncate font-medium">{active.name}</span>
				<ChevronDown className={cn("h-4 w-4 text-charcoal-blue-400 transition-transform", open && "rotate-180")} />
			</button>

			{open && (
				<div
					role="menu"
					className="menu-pop absolute right-0 top-full z-50 mt-2 w-56 overflow-hidden rounded-xl border border-charcoal-blue-200 bg-white p-1.5 shadow-2xl shadow-charcoal-blue-950/15 dark:border-white/10 dark:bg-charcoal-blue-950"
				>
					{households.map((household) => (
						<button
							key={household.id}
							type="button"
							role="menuitem"
							onClick={() => select(household.id)}
							className="flex w-full items-center gap-2 rounded-2xl px-3 py-2 text-left text-sm text-charcoal-blue-700 hover:bg-charcoal-blue-50 dark:text-charcoal-blue-200 dark:hover:bg-white/5"
						>
							<span className="min-w-0 flex-1">
								<span className="block truncate font-medium">{household.name}</span>
								<span className="block text-[11px] text-charcoal-blue-500 dark:text-charcoal-blue-400">
									{household.memberCount} member{household.memberCount === 1 ? "" : "s"}
								</span>
							</span>
							{household.id === active.id && (
								<AnimatedIcon name="circleCheck" size={14} aria-label="Active" />
							)}
						</button>
					))}
				</div>
			)}
		</div>
	);
}
