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
import { SPINE, USER_MENU, isActive, type NavItem } from "./nav";
import {
  LOG_ENTRY_EVENT,
  type LogEntryOptions,
} from "@/components/logging/LogEntryButton";

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
      className={mobile ? "bottom-nav-link" : "sidebar-link"}
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
    <div
      className={`shell-fullbleed app-shell${collapsed ? " sidebar-collapsed" : ""}${activeSession ? " active-session-shell" : ""}`}
    >
      <a className="skip-link" href="#app-content">
        Skip to content
      </a>
      <aside className="app-sidebar">
        <div className="sidebar-brand">
          <Link href="/today" aria-label="Mizan Today">
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
            className="icon-button"
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
        <div className="sidebar-log">
          <button
            className="btn-primary w-full"
            onClick={openLog}
            aria-label="Log an entry"
          >
            <Plus size={20} />
            {!collapsed && "Log entry"}
          </button>
        </div>
        <nav aria-label="Primary" className="sidebar-navigation">
          {SPINE.map((item) => (
            <NavigationLink key={item.href} item={item} collapsed={collapsed} />
          ))}
        </nav>
        <div className="sidebar-footer">
          {!collapsed && (
            <p className="text-xs log-muted">Meals. Lifts. Measurements.</p>
          )}
          <Link
            href="/profile"
            className="sidebar-profile"
            aria-label="Your profile"
          >
            <span className="user-initial" aria-hidden="true">
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
      <div className="app-column">
        <header className="app-topbar">
          <Link href="/today" className="mobile-brand">
            <Image src={logoTransparent} alt="" width={28} height={28} />
            <span>Mizan</span>
          </Link>
          <span className="desktop-context log-muted">
            {variant === "admin" ? "Administration" : "Your daily log"}
          </span>
          <div className="ml-auto flex items-center gap-2">
            <HouseholdSwitcher />
            <NotificationBell />
            <Popover open={accountOpen} onOpenChange={setAccountOpen}>
              <PopoverTrigger asChild>
                <button className="account-trigger" aria-label="Account">
                  <span className="user-initial" aria-hidden="true">
                    {(user.name || user.email).charAt(0).toUpperCase()}
                  </span>
                  <span className="account-name">
                    {user.name?.split(" ")[0] || "Account"}
                  </span>
                  <ChevronDown size={14} />
                </button>
              </PopoverTrigger>
              <PopoverContent align="end" className="account-popover">
                <div className="px-3 py-3 border-b mb-1">
                  <p className="font-medium text-sm truncate">
                    {user.name || "Your account"}
                  </p>
                  <p className="text-xs log-muted mt-1 break-all">
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
                    className="account-link"
                  >
                    <Icon name={item.icon} size={16} />
                    {item.label}
                  </Link>
                ))}
                <button
                  onClick={logout}
                  disabled={signingOut}
                  className="account-link text-destructive border-t mt-1"
                >
                  <LogOut size={16} />
                  {signingOut ? "Signing out…" : "Sign out"}
                </button>
              </PopoverContent>
            </Popover>
          </div>
        </header>
        <div id="app-content" tabIndex={-1} className="app-content">
          <div className="app-content-inner">{children}</div>
        </div>
        <nav aria-label="Primary mobile" className="mobile-navigation">
          {SPINE.slice(0, 2).map((item) => (
            <NavigationLink key={item.href} item={item} mobile />
          ))}
          <button
            className="mobile-log"
            onClick={openLog}
            aria-label="Log an entry"
          >
            <span>
              <Plus size={24} />
            </span>
            <span className="sr-only">Log</span>
          </button>
          {SPINE.slice(2).map((item) => (
            <NavigationLink key={item.href} item={item} mobile />
          ))}
        </nav>
      </div>
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
  );
}
