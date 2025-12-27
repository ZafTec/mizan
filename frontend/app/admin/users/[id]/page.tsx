import { redirect, notFound } from "next/navigation";
import { auth } from "@/lib/auth";
import { db } from "@/db/client";
import { users, sessions } from "@/db/schema";
import { eq, and, sql, count } from "drizzle-orm";
import Link from "next/link";
import { UserActions } from "./user-actions";

export const metadata = {
  title: "User Details | Admin",
  description: "View and manage user",
};

async function getUser(userId: string) {
  const [user] = await db
    .select({
      id: users.id,
      name: users.name,
      email: users.email,
      emailVerified: users.emailVerified,
      role: users.role,
      banned: users.banned,
      banReason: users.banReason,
      banExpires: users.banExpires,
      image: users.image,
      createdAt: users.createdAt,
      updatedAt: users.updatedAt,
    })
    .from(users)
    .where(eq(users.id, userId));

  if (!user) {
    return null;
  }

  const activeSessions = await db
    .select({ count: count() })
    .from(sessions)
    .where(
      and(
        eq(sessions.userId, userId),
        sql`${sessions.expiresAt} > NOW()`
      )
    )
    .then((res) => res[0]?.count || 0);

  const recentSessions = await db
    .select({
      id: sessions.id,
      token: sessions.token,
      ipAddress: sessions.ipAddress,
      userAgent: sessions.userAgent,
      createdAt: sessions.createdAt,
      expiresAt: sessions.expiresAt,
    })
    .from(sessions)
    .where(eq(sessions.userId, userId))
    .orderBy(sql`${sessions.createdAt} DESC`)
    .limit(5);

  return {
    ...user,
    activeSessions,
    recentSessions,
  };
}

export default async function UserDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  const session = await auth.api.getSession({
    headers: await import("next/headers").then((mod) => mod.headers()),
  });

  if (!session?.user) {
    redirect("/login");
  }

  if (session.user.role !== "admin") {
    redirect("/");
  }

  const user = await getUser(id);

  if (!user) {
    notFound();
  }

  const isSelf = session.user.id === user.id;

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
              <InfoRow label="Name" value={user.name || "—"} />
              <InfoRow label="Email" value={user.email} />
              <InfoRow
                label="Email Verified"
                value={user.emailVerified ? "Yes ✓" : "No"}
              />
              <InfoRow label="Role" value={user.role || "user"} />
              <InfoRow
                label="Status"
                value={user.banned ? `Banned - ${user.banReason || "No reason"}` : "Active"}
              />
              {user.banExpires && (
                <InfoRow
                  label="Ban Expires"
                  value={new Date(user.banExpires).toLocaleString()}
                />
              )}
              <InfoRow
                label="Created"
                value={
                  user.createdAt
                    ? new Date(user.createdAt).toLocaleString()
                    : "—"
                }
              />
              <InfoRow
                label="Updated"
                value={
                  user.updatedAt
                    ? new Date(user.updatedAt).toLocaleString()
                    : "—"
                }
              />
            </div>
          </div>

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
                        className={`text-xs px-2 py-1 rounded-full ${new Date(session.expiresAt) > new Date()
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
                value={user.banned ? "Banned" : "Active"}
                valueClassName={
                  user.banned ? "text-red-600" : "text-green-600"
                }
              />
            </div>
          </div>

          {session.user.role === "admin" && !isSelf && (
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
