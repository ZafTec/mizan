import { notFound, redirect } from "next/navigation";
import { getCurrentUser } from "@/lib/auth";
import { getAdminUser } from "@/data/admin/users";
import AdminPageHeader from "@/components/admin/AdminPageHeader";
import SupportNotesPanel from "@/components/admin/SupportNotesPanel";
import { listSupportNotesServer } from "@/lib/api/admin-ops.server";
import UsageThisMonth from "@/components/admin/platform/UsageThisMonth";
import { Pill } from "@/components/ui/data-table";
import { UserActions } from "./user-actions";

export const metadata = {
  title: "User | Admin",
  description: "One account: its details, actions, usage and support notes",
};

async function getUser(userId: string) {
  const detail = await getAdminUser(userId);
  if (!detail) return null;

  return {
    ...detail.user,
    activeSessions: detail.activeSessionCount,
    recentSessions: detail.recentSessions,
  };
}

function when(value?: string | Date | null) {
  return value ? new Date(value).toLocaleString() : "—";
}

/**
 * One account (docs/ADMIN.md#households-users-and-support-notes): the name
 * and email, a row of actions, the account's fields as ruled rows, then its
 * suspension, usage this month, recent sessions and support notes.
 */
export default async function UserDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  const admin = await getCurrentUser();
  if (!admin) redirect("/login");
  if (admin.role !== "admin") redirect("/");

  const [user, notes] = await Promise.all([
    getUser(id),
    listSupportNotesServer("User", id),
  ]);

  if (!user) {
    notFound();
  }

  const banEnded =
    user.banned &&
    user.banExpires !== null &&
    user.banExpires !== undefined &&
    new Date(user.banExpires) <= new Date();

  const isSelf = admin.id === user.id;
  const status = !user.banned
    ? "Active"
    : banEnded
      ? "Suspension ended"
      : "Suspended";

  const fields: { label: string; value: React.ReactNode; mono?: boolean }[] = [
    { label: "Role", value: user.role || "user" },
    { label: "Status", value: status },
    { label: "Email verified", value: user.emailVerified ? "Yes" : "No" },
    { label: "Open sessions", value: user.activeSessions },
    { label: "Joined", value: when(user.createdAt) },
    { label: "Updated", value: when(user.updatedAt) },
    { label: "ID", value: user.id, mono: true },
  ];

  return (
    <div className="flex flex-col gap-10 [&>header]:mb-0">
      <div className="flex flex-col gap-5">
        <AdminPageHeader
          title={user.name || "Unnamed"}
          lead={user.email}
          back={{ href: "/admin/users", label: "Users" }}
        />
        {isSelf ? (
          <p className="text-[14px] text-muted-foreground">
            This is your own account, so the account actions are not offered
            here.
          </p>
        ) : (
          <UserActions user={user} />
        )}
      </div>

      {user.banned && (
        <section aria-labelledby="suspension-title" className="flex flex-col">
          <div className="log-section-header">
            <h2
              id="suspension-title"
              className="text-[20px] font-semibold text-destructive"
            >
              {banEnded ? "Suspension ended" : "Suspended"}
            </h2>
          </div>
          <dl>
            <Field label="Reason">{user.banReason || "No reason given"}</Field>
            <Field label="Ends">
              {user.banExpires
                ? `${new Date(user.banExpires).toLocaleString("en-GB", { timeZone: "UTC" })} UTC`
                : "When an administrator lifts it"}
            </Field>
          </dl>
        </section>
      )}

      <section aria-labelledby="details-title" className="flex flex-col">
        <div className="log-section-header">
          <h2 id="details-title" className="text-[20px] font-semibold">
            Details
          </h2>
        </div>
        <dl>
          {fields.map((field) => (
            <Field key={field.label} label={field.label} mono={field.mono}>
              {field.value}
            </Field>
          ))}
        </dl>
      </section>

      <UsageThisMonth userId={user.id} />

      <section aria-labelledby="sessions-title" className="flex flex-col">
        <div className="log-section-header">
          <h2 id="sessions-title" className="text-[20px] font-semibold">
            Recent sessions
          </h2>
        </div>
        {user.recentSessions.length === 0 ? (
          <p className="log-empty text-[14px] text-muted-foreground">
            No sessions yet.
          </p>
        ) : (
          <ul>
            {user.recentSessions.map((session) => {
              const open = new Date(session.expiresAt) > new Date();
              return (
                <li key={session.id} className="ledger-row items-start">
                  <div className="flex min-w-0 flex-1 flex-col gap-0.5">
                    <span className="text-[14px] font-medium">
                      {session.ipAddress || "Unknown IP"}
                    </span>
                    <span className="truncate text-[12px] text-muted-foreground">
                      {session.userAgent || "Unknown device"}
                    </span>
                    <span className="text-[12px] text-muted-foreground tabular-nums">
                      Started {when(session.createdAt)} · ends{" "}
                      {when(session.expiresAt)}
                    </span>
                  </div>
                  <Pill tone={open ? "good" : "neutral"}>
                    {open ? "Open" : "Expired"}
                  </Pill>
                </li>
              );
            })}
          </ul>
        )}
      </section>

      <SupportNotesPanel
        subjectType="User"
        subjectId={user.id}
        initialNotes={notes}
      />
    </div>
  );
}

function Field({
  label,
  mono,
  children,
}: {
  label: string;
  mono?: boolean;
  children: React.ReactNode;
}) {
  return (
    <div className="grid min-h-11 gap-x-6 gap-y-0.5 border-b border-border py-2.5 sm:grid-cols-[180px_minmax(0,1fr)]">
      <dt className="text-[14px] text-muted-foreground">{label}</dt>
      <dd
        className={
          mono ? "font-mono text-[13px] break-all" : "text-[14px] break-words"
        }
      >
        {children}
      </dd>
    </div>
  );
}
