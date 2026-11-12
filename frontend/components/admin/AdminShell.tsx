"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, useId, useMemo, useRef, useState } from "react";
import {
  ADMIN_GROUPS,
  activeAdminHref,
  type AdminAttentionKey,
  type AdminSection,
} from "@/components/Layout/nav";
import {
  Drawer,
  DrawerBody,
  DrawerContent,
  DrawerDescription,
  DrawerHeader,
  DrawerTitle,
} from "@/components/ui/drawer";
import { Icon } from "@/components/ui/icon";
import { cn } from "@/lib/utils";

export type AdminAttentionCounts = Partial<Record<AdminAttentionKey, number>>;

/**
 * The admin console's own frame (docs/ADMIN.md#the-console): the sections in
 * groups down the left from the large breakpoint, a **Sections** sheet below
 * it, and a **Go to** box that finds a section or searches users and
 * households. A section with something waiting shows its count.
 */
export default function AdminShell({
  attention,
  children,
}: {
  attention: AdminAttentionCounts;
  children: React.ReactNode;
}) {
  const pathname = usePathname() ?? "/admin";
  const active = activeAdminHref(pathname);
  // The sheet remembers the page it was opened on, so navigating closes it.
  const [sheetPath, setSheetPath] = useState<string | null>(null);
  const sheetOpen = sheetPath === pathname;
  const setSheetOpen = (open: boolean) => setSheetPath(open ? pathname : null);
  const activeLabel =
    ADMIN_GROUPS.flatMap((g) => g.items).find((i) => i.href === active)
      ?.label ?? "Admin";

  return (
    <div className="shell-fullbleed flex h-dvh bg-background text-foreground">
      <a
        className="fixed -top-20 left-4 z-[200] bg-foreground px-[18px] py-3 whitespace-nowrap text-background focus:top-3"
        href="#admin-content"
      >
        Skip to content
      </a>
      <aside className="hidden w-[264px] shrink-0 flex-col overflow-y-auto border-r border-border px-3 pt-5 pb-8 lg:flex">
        <ConsoleHeader />
        <GoTo className="mx-2 mt-5" />
        <SectionList
          active={active}
          attention={attention}
          className="mt-5"
          itemClassName="h-9 rounded-[3px] px-2"
        />
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex min-h-14 items-center justify-between gap-3 border-b border-border pr-2 pl-5 lg:hidden">
          <Link href="/admin" className="text-[16px] font-semibold">
            Admin
          </Link>
          <button
            type="button"
            className="btn btn-secondary min-h-11 px-3"
            aria-expanded={sheetOpen}
            aria-haspopup="dialog"
            onClick={() => setSheetOpen(true)}
          >
            {activeLabel}
            <span className="text-muted-foreground"> · Sections</span>
          </button>
        </header>
        <main
          id="admin-content"
          tabIndex={-1}
          className="min-w-0 flex-1 overflow-y-auto px-5 pt-6 pb-16 outline-none sm:px-8 lg:px-10 lg:pt-8"
        >
          <div className="mx-auto max-w-[1060px]">{children}</div>
        </main>
      </div>

      <Drawer open={sheetOpen} onOpenChange={setSheetOpen} direction="bottom">
        <DrawerContent className="h-[85dvh]">
          <DrawerHeader>
            <div className="min-w-0 flex-1 pt-2">
              <DrawerTitle className="text-[20px]">Sections</DrawerTitle>
              <DrawerDescription className="sr-only">
                Every part of the admin console
              </DrawerDescription>
            </div>
          </DrawerHeader>
          <DrawerBody>
            <div className="flex flex-col gap-1 pb-3">
              <Link href="/today" className="text-link text-[13px]">
                Back to the app
              </Link>
            </div>
            <GoTo onNavigate={() => setSheetOpen(false)} />
            <SectionList
              active={active}
              attention={attention}
              className="mt-4"
              itemClassName="min-h-11 border-b border-border"
              onNavigate={() => setSheetOpen(false)}
            />
          </DrawerBody>
        </DrawerContent>
      </Drawer>
    </div>
  );
}

function ConsoleHeader() {
  return (
    <div className="flex flex-col gap-1 px-2">
      <Link href="/admin" className="text-[16px] font-semibold">
        Mizan admin
      </Link>
      <Link href="/today" className="text-link text-[13px]">
        Back to the app
      </Link>
    </div>
  );
}

function SectionList({
  active,
  attention,
  className,
  itemClassName,
  onNavigate,
}: {
  active: string | undefined;
  attention: AdminAttentionCounts;
  className?: string;
  itemClassName?: string;
  onNavigate?: () => void;
}) {
  return (
    <nav
      aria-label="Admin sections"
      className={cn("flex flex-col gap-5", className)}
    >
      {ADMIN_GROUPS.map((group) => (
        <div key={group.label} className="flex flex-col gap-0.5">
          <h2 className="px-2 pb-1 text-[12px] font-semibold text-muted-foreground">
            {group.label}
          </h2>
          {group.items.map((item) => (
            <SectionLink
              key={item.href}
              item={item}
              current={item.href === active}
              count={item.attention ? attention[item.attention] : undefined}
              className={itemClassName}
              onNavigate={onNavigate}
            />
          ))}
        </div>
      ))}
    </nav>
  );
}

function SectionLink({
  item,
  current,
  count,
  className,
  onNavigate,
}: {
  item: AdminSection;
  current: boolean;
  count?: number;
  className?: string;
  onNavigate?: () => void;
}) {
  return (
    <Link
      href={item.href}
      aria-current={current ? "page" : undefined}
      onClick={onNavigate}
      className={cn(
        "flex items-center justify-between gap-3 px-2 text-[14px] text-charcoal-blue-700 transition-colors duration-150 hover:bg-muted hover:text-foreground dark:text-charcoal-blue-200",
        current &&
          "bg-muted font-semibold text-foreground dark:text-foreground",
        className,
      )}
    >
      <span>{item.label}</span>
      {count ? (
        <span className="text-[12px] font-semibold tabular-nums text-destructive">
          {count}
          <span className="sr-only"> waiting</span>
        </span>
      ) : null}
    </Link>
  );
}

type Destination = { key: string; label: string; href: string };

/**
 * Finds a section by name, or hands the words to the users or households
 * search. Ctrl K (or Cmd K) puts the cursor here from anywhere in the console.
 */
function GoTo({
  className,
  onNavigate,
}: {
  className?: string;
  onNavigate?: () => void;
}) {
  const router = useRouter();
  const inputRef = useRef<HTMLInputElement>(null);
  const listId = useId();
  const [query, setQuery] = useState("");
  const [index, setIndex] = useState(0);

  useEffect(() => {
    function onKey(event: KeyboardEvent) {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "k") {
        const input = inputRef.current;
        if (!input || input.offsetParent === null) return;
        event.preventDefault();
        input.focus();
        input.select();
      }
    }
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, []);

  const results = useMemo<Destination[]>(() => {
    const words = query.trim();
    if (!words) return [];
    const needle = words.toLowerCase();
    const sections = ADMIN_GROUPS.flatMap((g) =>
      g.items
        .filter(
          (i) =>
            i.label.toLowerCase().includes(needle) ||
            g.label.toLowerCase().includes(needle),
        )
        .map((i) => ({ key: i.href, label: i.label, href: i.href })),
    );
    const term = encodeURIComponent(words);
    return [
      ...sections.slice(0, 6),
      {
        key: "users",
        label: `Users matching “${words}”`,
        href: `/admin/users?search=${term}`,
      },
      {
        key: "households",
        label: `Households matching “${words}”`,
        href: `/admin/households?search=${term}`,
      },
    ];
  }, [query]);

  function go(destination: Destination | undefined) {
    if (!destination) return;
    setQuery("");
    onNavigate?.();
    router.push(destination.href);
  }

  const open = results.length > 0;
  const activeId = open ? `${listId}-${index}` : undefined;

  return (
    <div className={cn("relative", className)}>
      <label className="flex flex-col gap-1.5">
        <span className="text-[12px] font-medium text-muted-foreground">
          Go to
        </span>
        <span className="flex min-h-11 items-center gap-2 rounded-[3px] border border-input bg-card px-2.5 focus-within:outline-2 focus-within:outline-offset-3 focus-within:outline-ring">
          <Icon
            name="search"
            className="size-4 shrink-0 text-muted-foreground"
          />
          <input
            ref={inputRef}
            type="search"
            role="combobox"
            aria-expanded={open}
            aria-controls={listId}
            aria-activedescendant={activeId}
            aria-autocomplete="list"
            placeholder="Section, user, household"
            value={query}
            onChange={(event) => {
              setQuery(event.target.value);
              setIndex(0);
            }}
            onKeyDown={(event) => {
              if (event.key === "ArrowDown") {
                event.preventDefault();
                setIndex((i) => Math.min(i + 1, results.length - 1));
              } else if (event.key === "ArrowUp") {
                event.preventDefault();
                setIndex((i) => Math.max(i - 1, 0));
              } else if (event.key === "Enter") {
                event.preventDefault();
                go(results[index]);
              } else if (event.key === "Escape") {
                setQuery("");
              }
            }}
            className="min-w-0 flex-1 bg-transparent text-[14px] outline-none placeholder:text-muted-foreground"
          />
          <kbd className="hidden rounded-[3px] border border-border px-1.5 text-[11px] font-medium text-muted-foreground lg:inline">
            Ctrl K
          </kbd>
        </span>
      </label>
      {open ? (
        <ul
          id={listId}
          role="listbox"
          aria-label="Destinations"
          className="absolute inset-x-0 top-full z-20 mt-1 animate-menu-pop overflow-hidden rounded-[3px] border border-foreground bg-card"
        >
          {results.map((result, i) => (
            <li
              key={result.key}
              id={`${listId}-${i}`}
              role="option"
              aria-selected={i === index}
              onMouseDown={(event) => {
                event.preventDefault();
                go(result);
              }}
              onMouseEnter={() => setIndex(i)}
              className={cn(
                "flex min-h-10 cursor-pointer items-center border-l-2 border-transparent px-3 text-[14px]",
                i === index && "border-foreground bg-muted",
              )}
            >
              {result.label}
            </li>
          ))}
        </ul>
      ) : null}
    </div>
  );
}
