import Link from "next/link";
import { notFound, redirect } from "next/navigation";
import { getCurrentUser } from "@/lib/auth";
import { getAdminUser } from "@/data/admin/users";
import SupportNotesPanel from "@/components/admin/SupportNotesPanel";
import { listSupportNotesServer } from "@/lib/api/admin-ops.server";
import { UserActions } from "./user-actions";

export const metadata = {
  title: "User Details | Admin",
  description: "View and manage user",
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

  return (
    <div className="container mx-auto px-4 py-8">
      <div className="mb-8 flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold mb-2">User Details</h1>
          <p className="text-muted-foreground">{user.email}</p>
        </div>
        <Link
          href="/admin/users"
          className="px-4 py-2 text-sm border rounded-lg hover:bg-accent"
        >
          ← Back to Users
        </Link>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div className="lg:col-span-2 space-y-6">
          <div className="bg-card rounded-lg border p-6">
            <h2 className="text-xl font-semibold mb-4">User Information</h2>
            <div className="space-y-4">
              <InfoRow label="ID" value={user.id} />
              <InfoRow label="Name" value={user.name || "-"} />
              <InfoRow label="Email" value={user.email} />
              <InfoRow
                label="Email Verified"
                value={user.emailVerified ? "Yes ✓" : "No"}
              />
              <InfoRow label="Role" value={user.role || "user"} />
              <InfoRow
                label="Status"
                value={
                  !user.banned
                    ? "Active"
                    : banEnded
                      ? "Suspension ended"
                      : "Suspended"
                }
              />
              <InfoRow
                label="Created"
                value={
                  user.createdAt
                    ? new Date(user.createdAt).toLocaleString()
                    : "-"
                }
              />
              <InfoRow
                label="Updated"
                value={
                  user.updatedAt
                    ? new Date(user.updatedAt).toLocaleString()
                    : "-"
                }
              />
            </div>
          </div>

          {user.banned && (
            <section
              aria-labelledby="suspension-title"
              className="border border-burnt-peach-300 bg-burnt-peach-50 p-6 dark:border-burnt-peach-500/30 dark:bg-burnt-peach-500/10"
            >
              <h2
                id="suspension-title"
                className="mb-3 text-xl font-semibold text-burnt-peach-800 dark:text-burnt-peach-300"
              >
                {banEnded ? "Suspension ended" : "Suspended"}
              </h2>
              <dl className="space-y-2 text-sm">
                <div>
                  <dt className="font-medium text-charcoal-blue-600 dark:text-charcoal-blue-300">
                    Reason
                  </dt>
                  <dd className="break-words">
                    {user.banReason || "No reason given"}
                  </dd>
                </div>
                <div>
                  <dt className="font-medium text-charcoal-blue-600 dark:text-charcoal-blue-300">
                    Ends
                  </dt>
                  <dd>
                    {user.banExpires
                      ? `${new Date(user.banExpires).toLocaleString("en-GB", { timeZone: "UTC" })} UTC`
                      : "When an administrator lifts it"}
                  </dd>
                </div>
              </dl>
            </section>
          )}

          <SupportNotesPanel
            subjectType="User"
            subjectId={user.id}
            initialNotes={notes}
          />

          <div className="bg-card rounded-lg border p-6">
            <h2 className="text-xl font-semibold mb-4">Recent Sessions</h2>
            <div className="space-y-4">
              {user.recentSessions.length === 0 ? (
                <p className="text-muted-foreground">No sessions found</p>
              ) : (
                user.recentSessions.map((session) => (
                  <div
                    key={session.id}
                    className="p-4 border rounded-lg space-y-2"
                  >
                    <div className="flex items-center justify-between">
                      <span className="text-sm font-medium">
                        {session.ipAddress || "Unknown IP"}
                      </span>
                      <span
                        className={`text-xs px-2 py-1 rounded-full ${
                          new Date(session.expiresAt) > new Date()
                            ? "bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200"
                            : "bg-gray-100 text-gray-800 dark:bg-gray-800 dark:text-gray-200"
                        }`}
                      >
                        {new Date(session.expiresAt) > new Date()
                          ? "Active"
                          : "Expired"}
                      </span>
                    </div>
                    <p className="text-sm text-muted-foreground truncate">
                      {session.userAgent || "Unknown device"}
                    </p>
                    <p className="text-xs text-muted-foreground">
                      Created: {new Date(session.createdAt).toLocaleString()}
                    </p>
                    <p className="text-xs text-muted-foreground">
                      Expires: {new Date(session.expiresAt).toLocaleString()}
                    </p>
                  </div>
                ))
              )}
            </div>
          </div>
        </div>

        <div className="space-y-6">
          <div className="bg-card rounded-lg border p-6">
            <h2 className="text-xl font-semibold mb-4">Quick Stats</h2>
            <div className="space-y-4">
              <StatItem label="Active Sessions" value={user.activeSessions} />
              <StatItem
                label="Account Status"
                value={user.banned ? "Suspended" : "Active"}
                valueClassName={user.banned ? "text-red-600" : "text-green-600"}
              />
            </div>
          </div>

          {!isSelf && (
            <div className="bg-card rounded-lg border p-6">
              <h2 className="text-xl font-semibold mb-4">Admin Actions</h2>
              <UserActions user={user} />
            </div>
          )}

          {isSelf && (
            <div className="bg-yellow-50 dark:bg-yellow-900/20 border border-yellow-200 dark:border-yellow-800 rounded-lg p-4">
              <p className="text-sm text-yellow-800 dark:text-yellow-200">
                This is your own account. Some admin actions are restricted.
              </p>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

function InfoRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex justify-between py-2 border-b last:border-b-0">
      <span className="font-medium text-muted-foreground">{label}</span>
      <span className="text-right break-all max-w-[60%]">{value}</span>
    </div>
  );
}

function StatItem({
  label,
  value,
  valueClassName,
}: {
  label: string;
  value: string | number;
  valueClassName?: string;
}) {
  return (
    <div className="flex justify-between items-center">
      <span className="text-sm text-muted-foreground">{label}</span>
      <span className={`text-lg font-semibold ${valueClassName || ""}`}>
        {value}
      </span>
    </div>
  );
}
