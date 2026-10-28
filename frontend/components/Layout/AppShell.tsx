"use client";

import Link from "next/link";
import Image from "next/image";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useEffect, useState } from "react";
import {
  ChevronDown,
  PanelLeftClose,
  PanelLeftOpen,
  Plus,
  LogOut,
} from "lucide-react";
import type { User } from "@/lib/auth";
import { signOut } from "@/lib/auth-client";
import { appToast } from "@/lib/toast";
import { clearAppearanceCookie } from "@/lib/appearance-cookie";
import { dateInTimeZone, isLogDate } from "@/lib/log-date";
import { Icon } from "@/components/ui/icon";
import { cn } from "@/lib/utils";
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover";
import { NotificationBell } from "@/components/NotificationBell";
import { GamificationToaster } from "@/components/gamification/GamificationToaster";
import { LOG_FEEDBACK_EVENT } from "@/lib/log-feedback";
import type { GamificationFeedback } from "@/types/gamification";
import logoTransparent from "@/public/logo_transparent.png";
import HouseholdSwitcher from "./HouseholdSwitcher";
import LogSheet from "./LogSheet";
import ImpersonationBanner from "./ImpersonationBanner";
import {
  AssistantLauncher,
  AssistantPanel,
  AssistantProvider,
} from "@/components/ai/Assistant";
import { SPINE, USER_MENU, isActive, type NavItem } from "./nav";
import {
  LOG_ENTRY_EVENT,
  type LogEntryOptions,
} from "@/components/logging/LogEntryButton";

const USER_INITIAL =
  "inline-flex size-[30px] shrink-0 items-center justify-center rounded-full border border-border bg-muted text-[12px] font-semibold text-foreground";
const ACCOUNT_LINK =
  "flex min-h-11 w-full items-center gap-2.5 px-3 py-2.5 text-[14px] hover:bg-muted hover:text-foreground";

function NavigationLink({
  item,
  collapsed,
  mobile,
}: {
  item: NavItem;
  collapsed?: boolean;
  mobile?: boolean;
}) {
  const active = isActive(usePathname(), item.href);
  return (
    <Link
      href={item.href}
      className={
        mobile
          ? "flex min-h-[52px] flex-1 flex-col items-center justify-center gap-[5px] text-[11px] text-muted-foreground aria-[current=page]:font-semibold aria-[current=page]:text-chart-1"
          : cn(
              "flex min-h-[46px] items-center gap-[13px] rounded-[3px] px-3.5 text-[14px] text-muted-foreground transition-[background-color,color] duration-160 ease-[ease-out] hover:bg-muted hover:text-foreground aria-[current=page]:bg-muted aria-[current=page]:font-semibold aria-[current=page]:text-foreground",
              collapsed && "justify-center px-0",
            )
      }
      aria-current={active ? "page" : undefined}
      aria-label={collapsed ? item.label : undefined}
      title={collapsed ? item.label : undefined}
    >
      <Icon name={item.icon} size={20} aria-hidden="true" />
      {!collapsed && <span>{item.label}</span>}
    </Link>
  );
}

export interface AppShellProps {
  user: User;
  children: React.ReactNode;
  variant?: "dashboard" | "admin";
}

export default function AppShell({
  user,
  children,
  variant = "dashboard",
}: AppShellProps) {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const [collapsed, setCollapsed] = useState(false);
  const [accountOpen, setAccountOpen] = useState(false);
  const [logOpen, setLogOpen] = useState(false);
  const [logOptions, setLogOptions] = useState<LogEntryOptions>({});
  const [signingOut, setSigningOut] = useState(false);
  const [feedback, setFeedback] = useState<GamificationFeedback>({});
  const activeSession = pathname === "/workout/active";
  const legacyMeal = searchParams.get("log") === "meal";
  const today = dateInTimeZone(user.timeZoneId || "UTC");
  const requestedDate =
    pathname === "/today" ? (searchParams.get("date") ?? undefined) : undefined;
  const logDate =
    isLogDate(requestedDate) && requestedDate <= today ? requestedDate : today;

  useEffect(() => {
    function open(event: Event) {
      setLogOptions((event as CustomEvent<LogEntryOptions>).detail || {});
      setLogOpen(true);
    }
    window.addEventListener(LOG_ENTRY_EVENT, open);
    function onFeedback(event: Event) {
      const next = (event as CustomEvent<GamificationFeedback>).detail;
      setFeedback((previous) => ({
        streak: next.streak?.extended ? next.streak : previous.streak,
        unlockedAchievements: [
          ...(previous.unlockedAchievements ?? []),
          ...(next.unlockedAchievements ?? []),
        ],
      }));
    }
    window.addEventListener(LOG_FEEDBACK_EVENT, onFeedback);
    return () => {
      window.removeEventListener(LOG_ENTRY_EVENT, open);
      window.removeEventListener(LOG_FEEDBACK_EVENT, onFeedback);
    };
  }, []);

  function openLog() {
    setLogOptions({ date: logDate });
    setLogOpen(true);
  }
  function closeLog() {
    setLogOpen(false);
    if (legacyMeal) {
      const next = new URLSearchParams(searchParams.toString());
      next.delete("log");
      router.replace(`${pathname}${next.size ? `?${next}` : ""}`, {
        scroll: false,
      });
    }
  }
  async function logout() {
    setSigningOut(true);
    try {
      await signOut();
      clearAppearanceCookie();
      router.push("/");
      router.refresh();
    } catch (error) {
      appToast.error(error, "Could not sign out");
      setSigningOut(false);
    }
  }

  return (
    <AssistantProvider userId={user.id}>
      <div
        className={cn(
          "shell-fullbleed group/shell flex h-dvh bg-background text-foreground",
          activeSession && "active-session-shell",
        )}
      >
        <a
          className="fixed -top-20 left-4 z-[200] bg-foreground px-[18px] py-3 text-background focus:top-3"
          href="#app-content"
        >
          Skip to content
        </a>
        <aside
          className={cn(
            "hidden flex-[0_0_224px] flex-col border-r border-border bg-background lg:flex",
            collapsed && "basis-20",
          )}
        >
          <div
            className={cn(
              "flex min-h-[72px] items-center gap-2 pr-4 pl-5",
              collapsed && "flex-col px-0 pt-4 pb-0",
            )}
          >
            <Link
              href="/today"
              aria-label="Mizan Today"
              className="flex items-center gap-[9px] text-[21px] font-semibold tracking-[-0.03em]"
            >
              <Image
                src={logoTransparent}
                alt=""
                width={32}
                height={32}
                priority
              />
              {!collapsed && <span>Mizan</span>}
            </Link>
            <button
              className={cn("icon-button", !collapsed && "ml-auto")}
              onClick={() => setCollapsed(!collapsed)}
              aria-label={collapsed ? "Expand sidebar" : "Collapse sidebar"}
            >
              {collapsed ? (
                <PanelLeftOpen size={17} />
              ) : (
                <PanelLeftClose size={17} />
              )}
            </button>
          </div>
          <div
            className={
              collapsed ? "px-3 pt-[18px] pb-4" : "px-[18px] pt-[22px] pb-4"
            }
          >
            <button
              className={cn("btn-primary w-full", collapsed && "px-0 py-3")}
              onClick={openLog}
              aria-label="Log an entry"
            >
              <Plus size={20} />
              {!collapsed && "Log entry"}
            </button>
          </div>
          <nav aria-label="Primary" className="grid gap-[5px] px-3">
            {SPINE.map((item) => (
              <NavigationLink
                key={item.href}
                item={item}
                collapsed={collapsed}
              />
            ))}
          </nav>
          <div className={cn("mt-auto", collapsed ? "p-4" : "p-5")}>
            {!collapsed && (
              <p className="text-xs text-muted-foreground">Meals. Lifts. Measurements.</p>
            )}
            <Link
              href="/profile"
              className={cn(
                "mt-[18px] flex min-h-11 items-center gap-2.5 border-t border-border pt-[18px]",
                collapsed && "justify-center",
              )}
              aria-label="Your profile"
            >
              <span className={USER_INITIAL} aria-hidden="true">
                {(user.name || user.email).charAt(0).toUpperCase()}
              </span>
              {!collapsed && (
                <span className="truncate text-sm">
                  {user.name || user.email}
                </span>
              )}
            </Link>
          </div>
        </aside>
        <div className="flex h-full min-w-0 flex-1 flex-col">
          {user.impersonation && (
            <ImpersonationBanner
              impersonation={user.impersonation}
              userName={user.name || user.email}
            />
          )}
          <header className="flex min-h-[72px] shrink-0 items-center gap-3 border-b border-border px-8 max-lg:min-h-16 max-lg:px-6 max-sm:px-[18px]">
            <Link
              href="/today"
              className="hidden items-center gap-2 text-[19px] font-semibold max-lg:flex"
            >
              <Image src={logoTransparent} alt="" width={28} height={28} />
              <span>Mizan</span>
            </Link>
            <span className="text-[13px] text-muted-foreground max-lg:hidden">
              {variant === "admin" ? "Administration" : "Your daily log"}
            </span>
            <div className="ml-auto flex items-center gap-2">
              <AssistantLauncher />
              <HouseholdSwitcher />
              <NotificationBell />
              <Popover open={accountOpen} onOpenChange={setAccountOpen}>
                <PopoverTrigger asChild>
                  <button
                    className="flex min-h-11 items-center gap-2 rounded-[3px] p-1.5 text-[13px] hover:bg-muted hover:text-foreground"
                    aria-label="Account"
                  >
                    <span className={USER_INITIAL} aria-hidden="true">
                      {(user.name || user.email).charAt(0).toUpperCase()}
                    </span>
                    <span className="max-sm:hidden">
                      {user.name?.split(" ")[0] || "Account"}
                    </span>
                    <ChevronDown size={14} />
                  </button>
                </PopoverTrigger>
                <PopoverContent
                  align="end"
                  className="w-[244px] rounded-[3px] p-1.5"
                >
                  <div className="px-3 py-3 border-b mb-1">
                    <p className="font-medium text-sm truncate">
                      {user.name || "Your account"}
                    </p>
                    <p className="text-xs text-muted-foreground mt-1 break-all">
                      {user.email}
                    </p>
                  </div>
                  {USER_MENU.filter(
                    (item) => !item.adminOnly || user.role === "admin",
                  ).map((item) => (
                    <Link
                      key={item.href}
                      href={item.href}
                      onClick={() => setAccountOpen(false)}
                      className={ACCOUNT_LINK}
                    >
                      <Icon name={item.icon} size={16} />
                      {item.label}
                    </Link>
                  ))}
                  <button
                    onClick={logout}
                    disabled={signingOut}
                    className={cn(ACCOUNT_LINK, "mt-1 border-t text-destructive")}
                  >
                    <LogOut size={16} />
                    {signingOut ? "Signing out…" : "Sign out"}
                  </button>
                </PopoverContent>
              </Popover>
            </div>
          </header>
          <div
            id="app-content"
            tabIndex={-1}
            className="min-h-0 flex-1 scroll-pt-6 overflow-auto px-10 pt-[38px] pb-12 max-xl:px-7 max-xl:pt-8 max-xl:pb-11 max-lg:pb-[calc(100px+env(safe-area-inset-bottom,0px))] max-sm:px-5 max-sm:pt-7"
          >
            <div className="mx-auto w-full max-w-[1240px]">{children}</div>
          </div>
          {/* The bottom bar belongs to the page, so a docked panel keeps it off its composer. */}
          <nav
            aria-label="Primary mobile"
            className="hidden max-lg:fixed max-lg:inset-x-0 max-lg:bottom-0 max-lg:z-30 max-lg:flex max-lg:items-stretch max-lg:border-t max-lg:border-border max-lg:bg-background max-lg:px-2.5 max-lg:pt-1.5 max-lg:pb-[calc(8px+env(safe-area-inset-bottom,0px))] md:max-lg:group-has-[>#assistant-panel:not([hidden])]/shell:end-[420px]"
          >
            {SPINE.slice(0, 2).map((item) => (
              <NavigationLink key={item.href} item={item} mobile />
            ))}
            <button
              className="flex flex-1 items-center justify-center"
              onClick={openLog}
              aria-label="Log an entry"
            >
              <span className="flex h-12 w-[50px] items-center justify-center rounded-[3px] bg-foreground text-background">
                <Plus size={24} />
              </span>
              <span className="sr-only">Log</span>
            </button>
            {SPINE.slice(2).map((item) => (
              <NavigationLink key={item.href} item={item} mobile />
            ))}
          </nav>
        </div>
        <AssistantPanel />
        <GamificationToaster {...feedback} />
        <LogSheet
          open={logOpen || legacyMeal}
          onClose={closeLog}
          initialKind={legacyMeal ? "meal" : logOptions.kind}
          initialDate={legacyMeal ? logDate : (logOptions.date ?? logDate)}
          initialMealType={logOptions.mealType}
          initialRecipe={logOptions.recipe}
        />
      </div>
    </AssistantProvider>
  );
}
