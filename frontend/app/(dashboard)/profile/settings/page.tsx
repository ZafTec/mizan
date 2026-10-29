"use client";

import { useEffect, useState } from "react";
import Image from "next/image";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ChevronRight } from "lucide-react";
import Loading from "@/components/Loading";
import ConfirmationModal from "@/components/ConfirmationModal";
import { useImageUpload } from "@/components/ImageUpload";
import { AiSettings } from "@/components/ai/AiSettings";
import { NotificationSettings } from "@/components/notifications/NotificationSettings";
import { ToggleRow } from "@/components/ui/toggle-row";
import {
  changePassword,
  deleteAccount,
  listSessions,
  useSession,
} from "@/lib/auth-client";
import { clientApi } from "@/lib/api.client";
import { downloadProfileExport } from "@/lib/api/profile";
import { useTheme } from "@/lib/hooks/useTheme";
import { useSaveStatus } from "@/lib/hooks/useSaveStatus";
import TimeZoneField from "@/components/appearance/TimeZoneField";
import PriceRegionField from "@/components/prices/PriceRegionField";
import { appToast } from "@/lib/toast";
import { cn } from "@/lib/utils";

const DELETE_CONFIRMATION_TEXT = "DELETE";

/** The page's sections, in order; the nav links to each and marks the one in view. */
const SECTIONS = [
  { id: "account", title: "Account" },
  { id: "day", title: "Your day and prices" },
  { id: "assistant", title: "Assistant" },
  { id: "notifications", title: "Notifications" },
  { id: "apps", title: "Connected apps" },
  { id: "appearance", title: "Appearance" },
  { id: "security", title: "Sign-in and security" },
  { id: "data", title: "Your data" },
] as const;

type SectionId = (typeof SECTIONS)[number]["id"];

/**
 * Every account setting on one ruled page (design board Settings.dc.html):
 * grouped sections with an anchor list, one row per setting, and changes that
 * save as they are made with "Saved" beside them. The assistant section is the
 * one the panel and the bots link to (#assistant).
 */
export default function ProfileSettingsPage() {
  const { data: session, isPending } = useSession();
  const router = useRouter();
  const active = useSectionInView();

  if (isPending) {
    return (
      <div className="flex min-h-[60vh] items-center justify-center">
        <Loading />
      </div>
    );
  }

  if (!session?.user) {
    router.push("/login");
    return null;
  }

  return (
    <div className="mx-auto max-w-[1120px]" data-testid="settings-page">
      <header className="mb-8 border-b border-foreground pb-5">
        <Link href="/profile" className="text-link">
          Profile
        </Link>
        <h1 className="mt-1 text-[30px] font-semibold leading-tight tracking-tight sm:text-[34px]">
          Settings
        </h1>
      </header>

      <div className="flex flex-col gap-8 lg:flex-row lg:items-start lg:gap-10">
        <nav
          aria-label="Settings sections"
          className="flex flex-wrap gap-1 text-sm lg:sticky lg:top-6 lg:w-[220px] lg:shrink-0 lg:flex-col lg:flex-nowrap"
        >
          {SECTIONS.map((section) => (
            <a
              key={section.id}
              href={`#${section.id}`}
              aria-current={active === section.id ? "location" : undefined}
              className={cn(
                "flex min-h-10 items-center rounded-[3px] px-2.5 transition-colors hover:bg-hair",
                active === section.id
                  ? "bg-hair font-medium text-foreground"
                  : "text-muted-foreground",
              )}
            >
              {section.title}
            </a>
          ))}
        </nav>

        <div className="min-w-0 flex-1 space-y-12">
          <AccountSection />
          <Section id="day" title="Your day and prices">
            <TimeZoneField current={session.user.timeZoneId ?? null} />
            <PriceRegionField />
          </Section>
          <Section
            id="assistant"
            title="Assistant"
            aside={
              <a href="#assistant-usage" className="text-link">
                Usage today
              </a>
            }
            lead="The same choices the assistant asks for in chat. Each one saves as you switch it."
          >
            <AiSettings />
          </Section>
          <NotificationsSection />
          <AppsSection />
          <AppearanceSection />
          <SecuritySection />
          <DataSection />
        </div>
      </div>
    </div>
  );
}

/** The section whose heading was last scrolled past the top third of the screen. */
function useSectionInView(): SectionId {
  const [active, setActive] = useState<SectionId>("account");
  useEffect(() => {
    const fromHash = SECTIONS.find(
      (section) => `#${section.id}` === window.location.hash,
    );
    // eslint-disable-next-line react-hooks/set-state-in-effect -- one read of the URL on mount
    if (fromHash) setActive(fromHash.id);
    if (typeof IntersectionObserver === "undefined") return;
    const observer = new IntersectionObserver(
      (entries) => {
        const visible = entries
          .filter((entry) => entry.isIntersecting)
          .sort((a, b) => a.boundingClientRect.top - b.boundingClientRect.top);
        const first = visible[0]?.target.id as SectionId | undefined;
        if (first) setActive(first);
      },
      { rootMargin: "0px 0px -66% 0px" },
    );
    for (const section of SECTIONS) {
      const element = document.getElementById(section.id);
      if (element) observer.observe(element);
    }
    return () => observer.disconnect();
  }, []);
  return active;
}

function Section({
  id,
  title,
  lead,
  aside,
  children,
}: {
  id: SectionId;
  title: string;
  lead?: string;
  aside?: React.ReactNode;
  children: React.ReactNode;
}) {
  return (
    <section
      id={id}
      aria-labelledby={`${id}-title`}
      className="setting-section"
    >
      <header>
        <h2 id={`${id}-title`} className="text-xl font-semibold">
          {title}
        </h2>
        {aside}
      </header>
      {lead && <p className="pt-3 text-sm text-muted-foreground">{lead}</p>}
      <div className={lead ? "pt-3" : undefined}>{children}</div>
    </section>
  );
}

function SavedNote({ text }: { text: string }) {
  return (
    <span
      role="status"
      className="min-w-12 shrink-0 text-xs whitespace-nowrap text-brand-700 dark:text-brand-300"
    >
      <span
        key={text}
        className="inline-block transition-opacity duration-160 ease-out starting:opacity-0"
      >
        {text}
      </span>
    </span>
  );
}

/** One PUT /api/Users/me for the account fields; the users table has a single owner. */
function saveAccount(body: { name?: string | null; image?: string | null }) {
  return clientApi("/api/Users/me", { method: "PUT", body });
}

function AccountSection() {
  const { data: session } = useSession();
  const router = useRouter();
  const user = session!.user;
  const [name, setName] = useState(user.name ?? "");
  const [savedName, setSavedName] = useState(user.name ?? "");
  const [image, setImage] = useState(user.image ?? "");
  const [savedImage, setSavedImage] = useState(user.image ?? "");
  const nameStatus = useSaveStatus();
  const imageStatus = useSaveStatus();

  const avatarUpload = useImageUpload({
    folder: "avatars",
    onUploaded: (url) => void saveImage(url),
  });

  async function saveName() {
    const next = name.trim();
    if (next === savedName.trim()) return;
    try {
      await nameStatus.track(() => saveAccount({ name: next || null }));
      setSavedName(next);
      router.refresh();
    } catch (error) {
      appToast.error(error, "Could not save your name");
    }
  }

  async function saveImage(next: string) {
    const trimmed = next.trim();
    setImage(trimmed);
    if (trimmed === savedImage) return;
    try {
      await imageStatus.track(() => saveAccount({ image: trimmed || null }));
      setSavedImage(trimmed);
      router.refresh();
    } catch (error) {
      setImage(savedImage);
      appToast.error(error, "Could not save your photo");
    }
  }

  const uploading = avatarUpload.uploading || imageStatus.status === "saving";

  return (
    <Section
      id="account"
      title="Account"
      aside={
        <span className="text-xs text-muted-foreground">
          Changes save as you go
        </span>
      }
    >
      <form
        className="setting-row"
        onSubmit={(event) => {
          event.preventDefault();
          void saveName();
        }}
      >
        <label htmlFor="settings-name" className="setting-label">
          Display name
        </label>
        <div className="flex items-center gap-3">
          <input
            id="settings-name"
            type="text"
            className="input min-w-0 flex-1"
            value={name}
            autoComplete="name"
            onChange={(event) => setName(event.target.value)}
            onBlur={() => void saveName()}
            placeholder="Your name"
          />
          <SavedNote text={nameStatus.text} />
        </div>
      </form>

      <div className="setting-row">
        <span className="setting-label">Email</span>
        <div className="min-w-0">
          <p className="truncate text-[15px]">{user.email}</p>
          <p className="text-xs text-muted-foreground">
            Email changes are not available yet.
          </p>
        </div>
      </div>

      <div className="setting-row">
        <span className="setting-label">Photo</span>
        <div className="flex flex-wrap items-center gap-3">
          <Avatar image={image} fallback={name || user.email} />
          {avatarUpload.input}
          <button
            type="button"
            onClick={avatarUpload.open}
            disabled={uploading}
            className="btn-secondary min-h-11"
          >
            {uploading ? "Saving photo…" : "Upload a photo"}
          </button>
          <SavedNote text={imageStatus.text} />
        </div>
      </div>

      <form
        className="setting-row"
        onSubmit={(event) => {
          event.preventDefault();
          void saveImage(image);
        }}
      >
        <label htmlFor="settings-image" className="setting-label">
          Photo address
        </label>
        <input
          id="settings-image"
          type="url"
          inputMode="url"
          className="input w-full"
          value={image}
          onChange={(event) => setImage(event.target.value)}
          onBlur={() => void saveImage(image)}
          placeholder="https://…"
        />
      </form>
    </Section>
  );
}

function Avatar({ image, fallback }: { image: string; fallback: string }) {
  if (image)
    return (
      <span className="relative h-11 w-11 shrink-0 overflow-hidden rounded-full">
        <Image src={image} alt="" fill sizes="44px" className="object-cover" />
      </span>
    );
  return (
    <span
      aria-hidden="true"
      className="flex h-11 w-11 shrink-0 items-center justify-center rounded-full bg-hair font-semibold"
    >
      {fallback.charAt(0).toUpperCase()}
    </span>
  );
}

function LinkRow({
  href,
  title,
  detail,
  action,
}: {
  href: string;
  title: string;
  detail: string;
  action: string;
}) {
  return (
    <div className="ledger-row">
      <span className="min-w-0 flex-1">
        <span className="block text-[15px] font-medium">{title}</span>
        <span className="mt-0.5 block text-[13px] text-muted-foreground">
          {detail}
        </span>
      </span>
      <Link
        href={href}
        className="text-link"
        aria-label={`${action}: ${title}`}
      >
        {action}
        <ChevronRight aria-hidden="true" size={16} strokeWidth={1.7} />
      </Link>
    </div>
  );
}

function AppsSection() {
  return (
    <Section id="apps" title="Connected apps">
      <LinkRow
        href="/profile/settings/telegram"
        title="Telegram"
        detail="Log meals from a photo, see today's plan and get notifications in the chat."
        action="Manage"
      />
      <LinkRow
        href="/profile/mcp"
        title="AI apps"
        detail="Claude and other assistants you connect, and what each may do."
        action="Permissions"
      />
      <LinkRow
        href="/profile/household"
        title="Household"
        detail="Share meal plans and shopping lists with the people you live with."
        action="Manage"
      />
    </Section>
  );
}

const THEMES = [
  { value: "light", label: "Light" },
  { value: "dark", label: "Dark" },
  { value: "system", label: "Device" },
] as const;

function NotificationsSection() {
  const status = useSaveStatus();
  return (
    <Section
      id="notifications"
      title="Notifications"
      aside={<SavedNote text={status.text} />}
      lead="Choose what reaches each channel. News and offers stay off until you turn them on."
    >
      <NotificationSettings status={status} />
    </Section>
  );
}

function AppearanceSection() {
  const { settings, persistSettings } = useTheme();
  const status = useSaveStatus();

  async function change(updates: Parameters<typeof persistSettings>[0]) {
    try {
      await status.track(() => persistSettings(updates));
    } catch (error) {
      appToast.error(error, "Could not save your appearance");
    }
  }

  return (
    <Section
      id="appearance"
      title="Appearance"
      aside={<SavedNote text={status.text} />}
    >
      <div className="setting-row">
        <span id="theme-label" className="setting-label">
          Theme
        </span>
        <div
          role="radiogroup"
          aria-labelledby="theme-label"
          className="inline-flex w-full max-w-sm border border-input sm:w-auto"
        >
          {THEMES.map((option) => (
            <button
              key={option.value}
              type="button"
              role="radio"
              aria-checked={settings.theme === option.value}
              onClick={() => void change({ theme: option.value })}
              className={cn(
                "min-h-11 flex-1 border-l border-input px-4 text-sm first:border-l-0 sm:flex-none",
                settings.theme === option.value
                  ? "bg-foreground font-medium text-background"
                  : "text-muted-foreground hover:bg-hair",
              )}
            >
              {option.label}
            </button>
          ))}
        </div>
      </div>
      <div className="border-b border-border">
        <ToggleRow
          label="Compact mode"
          description="Tighter spacing for denser pages."
          checked={settings.compactMode}
          onChange={(compactMode) => void change({ compactMode })}
        />
        <ToggleRow
          label="Reduce animations"
          description="Keep motion to a minimum across the app."
          checked={settings.reduceAnimations}
          onChange={(reduceAnimations) => void change({ reduceAnimations })}
        />
      </div>
    </Section>
  );
}

function SecuritySection() {
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [changing, setChanging] = useState(false);
  const [sessionCount, setSessionCount] = useState<number | null>(null);

  async function countSessions() {
    try {
      const sessions = await listSessions();
      setSessionCount(
        sessions.filter((item) => new Date(item.expiresAt) > new Date()).length,
      );
    } catch {
      setSessionCount(null);
    }
  }

  useEffect(() => {
    void countSessions();
  }, []);

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    if (newPassword !== confirmPassword) {
      appToast.error("Passwords do not match");
      return;
    }
    if (newPassword.length < 10) {
      appToast.error("Password must be at least 10 characters");
      return;
    }
    setChanging(true);
    try {
      await changePassword(currentPassword, newPassword);
      setCurrentPassword("");
      setNewPassword("");
      setConfirmPassword("");
      appToast.success("Password updated. Other sessions were signed out.");
      await countSessions();
    } catch (error) {
      appToast.error(
        error,
        "Password change failed. Check your current password.",
      );
    } finally {
      setChanging(false);
    }
  }

  const field = (
    id: string,
    label: string,
    value: string,
    set: (value: string) => void,
    autoComplete: string,
  ) => (
    <div className="setting-row">
      <label htmlFor={id} className="setting-label">
        {label}
      </label>
      <input
        id={id}
        type="password"
        className="input w-full"
        value={value}
        autoComplete={autoComplete}
        onChange={(event) => set(event.target.value)}
      />
    </div>
  );

  return (
    <Section id="security" title="Sign-in and security">
      <LinkRow
        href="/profile/sessions"
        title="Signed-in devices"
        detail={
          sessionCount === null
            ? "Where you are signed in"
            : `${sessionCount} active session${sessionCount === 1 ? "" : "s"}`
        }
        action="Manage"
      />
      <form onSubmit={submit} aria-labelledby="password-title" className="pt-6">
        <h3 id="password-title" className="text-sm font-semibold">
          Change password
        </h3>
        {field(
          "current-password",
          "Current password",
          currentPassword,
          setCurrentPassword,
          "current-password",
        )}
        {field(
          "new-password",
          "New password",
          newPassword,
          setNewPassword,
          "new-password",
        )}
        {field(
          "confirm-password",
          "Confirm new password",
          confirmPassword,
          setConfirmPassword,
          "new-password",
        )}
        <div className="flex flex-wrap items-center gap-3 pt-4">
          <button
            type="submit"
            disabled={
              changing || !currentPassword || !newPassword || !confirmPassword
            }
            className="btn-primary min-h-11"
          >
            {changing ? "Updating…" : "Change password"}
          </button>
          <p className="text-xs text-muted-foreground">
            At least 10 characters. Your other sessions are signed out.
          </p>
        </div>
      </form>
    </Section>
  );
}

function DataSection() {
  const [exporting, setExporting] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [typed, setTyped] = useState("");
  const [showModal, setShowModal] = useState(false);

  async function exportData() {
    setExporting(true);
    try {
      const { blob, filename } = await downloadProfileExport();
      const objectUrl = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = objectUrl;
      anchor.download = filename;
      document.body.appendChild(anchor);
      anchor.click();
      anchor.remove();
      URL.revokeObjectURL(objectUrl);
      appToast.success("Your export is downloading");
    } catch (error) {
      appToast.error(error, "Could not export your data");
    } finally {
      setExporting(false);
    }
  }

  async function remove() {
    setShowModal(false);
    setDeleting(true);
    try {
      await deleteAccount();
      window.location.href = "/";
    } catch (error) {
      appToast.error(
        error,
        "Account deletion failed. Sign in again and try once more.",
      );
      setDeleting(false);
    }
  }

  return (
    <Section id="data" title="Your data">
      <div id="export" className="ledger-row setting-section">
        <span className="min-w-0 flex-1">
          <span className="block text-[15px]">
            Download everything you have logged
          </span>
          <span className="mt-0.5 block text-[13px] text-muted-foreground">
            One JSON file: profile, goals, meals, plans, measurements, workouts,
            recipes and connected-app usage.
          </span>
        </span>
        <button
          type="button"
          onClick={exportData}
          disabled={exporting}
          className="btn-secondary min-h-11"
        >
          {exporting ? "Preparing…" : "Export"}
        </button>
      </div>
      <div className="ledger-row flex-wrap">
        <span className="min-w-0 flex-1 text-[15px]">
          Delete your account and all your data
        </span>
        {!confirming && (
          <button
            type="button"
            onClick={() => setConfirming(true)}
            className="btn-ghost-destructive min-h-11"
          >
            Delete account
          </button>
        )}
        {confirming && (
          <form
            className="flex w-full flex-wrap items-end gap-3 pb-1"
            onSubmit={(event) => {
              event.preventDefault();
              if (typed === DELETE_CONFIRMATION_TEXT) setShowModal(true);
            }}
          >
            <div className="min-w-0 flex-1 basis-48">
              <label htmlFor="delete-confirmation" className="label">
                Type {DELETE_CONFIRMATION_TEXT} to confirm
              </label>
              <input
                id="delete-confirmation"
                type="text"
                autoComplete="off"
                className="input w-full"
                value={typed}
                onChange={(event) => setTyped(event.target.value)}
                autoFocus
              />
            </div>
            <button
              type="submit"
              disabled={deleting || typed !== DELETE_CONFIRMATION_TEXT}
              className="btn-danger min-h-11"
            >
              {deleting ? "Deleting…" : "Delete permanently"}
            </button>
            <button
              type="button"
              className="btn-ghost min-h-11"
              onClick={() => {
                setConfirming(false);
                setTyped("");
              }}
            >
              Cancel
            </button>
          </form>
        )}
      </div>
      <ConfirmationModal
        isOpen={showModal}
        onClose={() => setShowModal(false)}
        onConfirm={remove}
        title="Delete account permanently?"
        message="All your meals, recipes, workouts, and progress will be erased. This cannot be undone."
        confirmText="Yes, delete my account"
        cancelText="Keep account"
        isDanger
        isLoading={deleting}
      />
    </Section>
  );
}
