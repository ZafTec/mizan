import { redirect } from "next/navigation";
import { auth } from "@/lib/auth";
import { db } from "@/db/client";
import { users, sessions, foods } from "@/db/schema";
import { eq, sql, count, gte } from "drizzle-orm";
import LiveAuditLog from "./LiveAuditLog";
import { getAuditLogs } from "@/data/audit";


export const metadata = {
  title: "Admin Dashboard | Macro Chef",
  description: "System administration dashboard",
};

async function getAdminStats() {
  const totalUsers = await db
    .select({ count: count() })
    .from(users)
    .then((res) => res[0]?.count || 0);

  const activeTrainers = await db
    .select({ count: count() })
    .from(users)
    .where(eq(users.role, "trainer"))
    .then((res) => res[0]?.count || 0);

  const bannedUsers = await db
    .select({ count: count() })
    .from(users)
    .where(eq(users.banned, true))
    .then((res) => res[0]?.count || 0);

  const activeSessions = await db
    .select({ count: count() })
    .from(sessions)
    .where(sql`${sessions.expiresAt} > NOW()`)
    .then((res) => res[0]?.count || 0);

  const recentUsers = await db
    .select({
      id: users.id,
      name: users.name,
      email: users.email,
      role: users.role,
      createdAt: users.createdAt,
    })
    .from(users)
    .orderBy(sql`${users.createdAt} DESC`)
    .limit(5);

  const totalFoods = await db
    .select({ count: count() })
    .from(foods)
    .then((res) => res[0]?.count || 0);

  const yesterday = new Date();
  yesterday.setHours(yesterday.getHours() - 24);

  const recentAuditLogs = await getAuditLogs({
    pageSize: 1,
  }).then(res => res.totalCount);

  return {
    totalUsers,
    activeTrainers,
    bannedUsers,
    activeSessions,
    recentUsers,
    totalFoods,
    recentAuditLogs
  };
}

export default async function AdminDashboard() {
  const session = await auth.api.getSession({
    headers: await import("next/headers").then((mod) => mod.headers()),
  });

  if (!session?.user) {
    redirect("/login");
  }

  if (session.user.role !== "admin") {
    redirect("/");
  }

  const stats = await getAdminStats();

  return (
    <div className="container mx-auto px-4 py-8">
      <div className="mb-8">
        <h1 className="text-3xl font-bold mb-2">Admin Dashboard</h1>
        <p className="text-muted-foreground">
          System overview and quick actions
        </p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6 gap-6 mb-8">
        <StatCard
          title="Total Users"
          value={stats.totalUsers}
          link="/admin/users"
          linkText="Manage users"
        />
        <StatCard
          title="Active Trainers"
          value={stats.activeTrainers}
          link="/admin/users?role=trainer"
          linkText="View trainers"
        />
        <StatCard
          title="Banned Users"
          value={stats.bannedUsers}
          link="/admin/users?banned=true"
          linkText="View banned"
        />
        <StatCard
          title="Public Ingredients"
          value={stats.totalFoods}
          link="/admin/ingredients"
          linkText="Manage foods"
        />
        <StatCard
          title="Recent Activity"
          value={stats.recentAuditLogs}
          link="/admin/audit-logs"
          linkText="View logs"
        />
        <StatCard
          title="Active Sessions"
          value={stats.activeSessions}
          link="/admin/sessions"
          linkText="Manage sessions"
        />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 xl:grid-cols-4 gap-8">
        <div className="lg:col-span-2 bg-card rounded-lg border p-6">
          <h2 className="text-xl font-semibold mb-4">Recent Users</h2>
          <div className="space-y-4">
            {stats.recentUsers.map((user) => (
              <div
                key={user.id}
                className="flex items-center justify-between py-3 border-b last:border-b-0"
              >
                <div>
                  <p className="font-medium">{user.name || "Unnamed User"}</p>
                  <p className="text-sm text-muted-foreground">{user.email}</p>
                </div>
                <div className="flex items-center gap-4">
                  <span className="text-xs px-2 py-1 rounded-full bg-primary/10 text-primary">
                    {user.role}
                  </span>
                  <a
                    href={`/admin/users/${user.id}`}
                    className="text-sm text-primary hover:underline"
                  >
                    View
                  </a>
                </div>
              </div>
            ))}
          </div>
        </div>

        <div className="lg:col-span-1 xl:col-span-2 min-h-125">
          <LiveAuditLog />
        </div>
      </div>

      <div className="mt-8 grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        <QuickActionCard
          title="User Management"
          description="Manage users, roles, and permissions"
          link="/admin/users"
          icon="👥"
        />
        <QuickActionCard
          title="Session Management"
          description="View and manage active user sessions"
          link="/admin/sessions"
          icon="🔐"
        />
        <QuickActionCard
          title="Trainer-Client Relationships"
          description="Monitor and manage trainer-client connections"
          link="/admin/relationships"
          icon="🤝"
        />
        <QuickActionCard
          title="Ingredient Management"
          description="Add, edit, and verify public food ingredients"
          link="/admin/ingredients"
          icon="🍎"
        />
        <QuickActionCard
          title="Audit Logs"
          description="View system-wide activity and security logs"
          link="/admin/audit-logs"
          icon="📋"
        />
      </div>
    </div>
  );
}

function StatCard({
  title,
  value,
  link,
  linkText,
}: {
  title: string;
  value: number;
  link: string;
  linkText: string;
}) {
  return (
    <div className="bg-card rounded-lg border p-6">
      <h3 className="text-sm font-medium text-muted-foreground mb-2">
        {title}
      </h3>
      <p className="text-3xl font-bold mb-4">{value}</p>
      <a href={link} className="text-sm text-primary hover:underline">
        {linkText} →
      </a>
    </div>
  );
}

function QuickActionCard({
  title,
  description,
  link,
  icon,
}: {
  title: string;
  description: string;
  link: string;
  icon: string;
}) {
  return (
    <a
      href={link}
      className="bg-card rounded-lg border p-6 hover:border-primary transition-colors"
    >
      <div className="text-4xl mb-4">{icon}</div>
      <h3 className="text-lg font-semibold mb-2">{title}</h3>
      <p className="text-sm text-muted-foreground">{description}</p>
    </a>
  );
}
