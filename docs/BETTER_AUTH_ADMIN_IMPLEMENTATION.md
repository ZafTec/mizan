# BetterAuth Admin Plugin Implementation Guide

**Project:** MacroChef (Mizan)
**Date:** 2025-12-26
**Status:** Implementation Ready

## Overview

This document provides a comprehensive guide for implementing the BetterAuth Admin plugin in the MacroChef application. The admin plugin is **already configured** in the codebase but lacks custom access control and full feature utilization.

## Current Status

### Already Implemented ✅

**Server Configuration** (`frontend/lib/auth.ts`):
```typescript
admin({
  defaultRole: "user",
  adminRoles: ["admin"], // Only system admins, NOT trainers
  impersonationSessionDuration: 60 * 60 * 24, // 24 hours
  allowImpersonatingAdmins: false,
})
```

**Client Configuration** (`frontend/lib/auth-client.ts`):
```typescript
import { adminClient } from "better-auth/client/plugins";

export const authClient = createAuthClient({
  plugins: [
    jwtClient(),
    organizationClient(),
    adminClient(), // Already added
  ],
});
```

**Database Schema** (`frontend/db/schema.ts`):
```typescript
export const users = pgTable("users", {
  id: uuid("id").defaultRandom().primaryKey(),
  email: varchar("email", { length: 255 }).notNull().unique(),
  emailVerified: boolean("email_verified").default(false),
  name: varchar("name", { length: 255 }),
  image: text("image"),
  role: varchar("role", { length: 50 }).default("user"), // ✅ Already has role field
  banned: boolean("banned").default(false), // ✅ Already has ban fields
  banReason: text("ban_reason"),
  banExpires: timestamp("ban_expires", { withTimezone: true }),
  createdAt: timestamp("created_at", { withTimezone: true }).defaultNow(),
  updatedAt: timestamp("updated_at", { withTimezone: true }).defaultNow(),
});

export const sessions = pgTable("sessions", {
  id: uuid("id").defaultRandom().primaryKey(),
  userId: uuid("user_id").references(() => users.id, { onDelete: "cascade" }).notNull(),
  token: varchar("token", { length: 255 }).notNull().unique(),
  expiresAt: timestamp("expires_at", { withTimezone: true }).notNull(),
  ipAddress: text("ip_address"),
  userAgent: text("user_agent"),
  impersonatedBy: uuid("impersonated_by"), // ✅ Already has impersonation field
  createdAt: timestamp("created_at", { withTimezone: true }).defaultNow().notNull(),
  updatedAt: timestamp("updated_at", { withTimezone: true }).defaultNow().notNull(),
});
```

### Database Migration Status

**CRITICAL:** Run migration to ensure all admin plugin fields exist:

```bash
cd frontend
bun run db:generate
bun run db:migrate
```

The schema already has all required fields:
- `users.role` - User role field
- `users.banned`, `users.banReason`, `users.banExpires` - Ban management
- `sessions.impersonatedBy` - Impersonation tracking

## Admin Plugin Features

### 1. User Management

#### Create User
```typescript
const { data, error } = await authClient.admin.createUser({
  email: "user@example.com",
  password: "SecurePassword123!",
  name: "John Doe",
  role: "user", // or "admin", "trainer"
  data: {
    // Additional custom fields from your schema
    customField: "value"
  }
});
```

#### Update User
```typescript
const { data, error } = await authClient.admin.updateUser({
  userId: "user-id",
  data: {
    name: "Updated Name",
    email: "newemail@example.com",
    // Any other user fields
  }
});
```

#### Set User Password
```typescript
await authClient.admin.setUserPassword({
  userId: "user-id",
  newPassword: "NewSecurePassword123!"
});
```

#### Set User Role
```typescript
await authClient.admin.setRole({
  userId: "user-id",
  role: "admin" // or ["admin", "trainer"] for multiple roles
});
```

### 2. Ban/Unban Users

#### Ban User
```typescript
await authClient.admin.banUser({
  userId: "user-id",
  banReason: "Violation of terms of service",
  banExpiresIn: 60 * 60 * 24 * 7 // 7 days in seconds (optional - omit for permanent ban)
});
```

#### Unban User
```typescript
await authClient.admin.unbanUser({
  userId: "user-id"
});
```

### 3. Session Management

#### Revoke Specific Session
```typescript
await authClient.admin.revokeUserSession({
  sessionToken: "session_token_here"
});
```

#### Revoke All User Sessions
```typescript
await authClient.admin.revokeUserSessions({
  userId: "user-id"
});
```

### 4. User Impersonation

#### Start Impersonation
```typescript
const impersonationSession = await authClient.admin.impersonateUser({
  userId: "user-id"
});

// Now acting as the impersonated user
// Session lasts 24 hours (configured in server config)
```

#### Stop Impersonation
```typescript
await authClient.admin.stopImpersonating();
// Returns to original admin session
```

### 5. List Users (Powerful Search/Filter)

```typescript
const { data } = await authClient.admin.listUsers({
  // Search
  searchValue: "john",
  searchField: "name", // or "email"
  searchOperator: "contains", // or "starts_with", "ends_with"

  // Pagination
  limit: 50,
  offset: 0,

  // Sorting
  sortBy: "createdAt",
  sortDirection: "desc", // or "asc"

  // Filtering
  filterField: "role",
  filterValue: "admin",
  filterOperator: "eq", // or "ne", "lt", "lte", "gt", "gte"
});

// Response:
// {
//   users: User[],
//   total: number,
//   limit: number,
//   offset: number
// }
```

## Custom Access Control (Optional - Advanced)

For fine-grained permissions beyond simple role checking, implement custom access control.

### Step 1: Define Access Control Schema

Create `frontend/lib/permissions.ts`:

```typescript
import { createAccessControl } from "better-auth/plugins/access";
import { defaultStatements, adminAc } from "better-auth/plugins/admin/access";

// Define your custom resources and actions
export const statement = {
  ...defaultStatements, // Include default admin permissions
  user: ["create", "read", "update", "delete", "ban"],
  recipe: ["create", "read", "update", "delete"],
  mealPlan: ["create", "read", "update", "delete"],
  workout: ["create", "read", "update", "delete"],
  household: ["create", "read", "update", "delete", "invite"],
  trainerClient: ["create", "read", "update", "delete", "message"],
} as const;

export const ac = createAccessControl(statement);

// Define roles with specific permissions
export const userRole = ac.newRole({
  user: ["read", "update"], // Can only read/update own profile
  recipe: ["create", "read", "update", "delete"],
  mealPlan: ["create", "read", "update", "delete"],
  workout: ["create", "read", "update", "delete"],
  household: ["create", "read", "update", "invite"],
});

export const trainerRole = ac.newRole({
  user: ["read"], // Can read client profiles
  recipe: ["create", "read", "update", "delete"],
  mealPlan: ["create", "read", "update", "delete"], // Can manage client meal plans
  workout: ["create", "read", "update", "delete"], // Can manage client workouts
  trainerClient: ["create", "read", "update", "delete", "message"],
});

export const adminRole = ac.newRole({
  ...adminAc.statements, // Include all default admin permissions
  user: ["create", "read", "update", "delete", "ban"],
  recipe: ["create", "read", "update", "delete"],
  mealPlan: ["create", "read", "update", "delete"],
  workout: ["create", "read", "update", "delete"],
  household: ["create", "read", "update", "delete", "invite"],
  trainerClient: ["create", "read", "update", "delete"],
});
```

### Step 2: Update Server Config

Update `frontend/lib/auth.ts`:

```typescript
import { ac, adminRole, trainerRole, userRole } from "@/lib/permissions";

export const auth = betterAuth({
  // ... existing config
  plugins: [
    jwt({ /* ... */ }),
    organization({ /* ... */ }),
    admin({
      defaultRole: "user",
      ac, // Add access controller
      roles: {
        admin: adminRole,
        trainer: trainerRole,
        user: userRole,
      },
      impersonationSessionDuration: 60 * 60 * 24,
      allowImpersonatingAdmins: false,
    }),
  ],
});
```

### Step 3: Update Client Config

Update `frontend/lib/auth-client.ts`:

```typescript
import { ac, adminRole, trainerRole, userRole } from "@/lib/permissions";

export const authClient = createAuthClient({
  baseURL: process.env.NEXT_PUBLIC_APP_URL || "http://localhost:3000",
  plugins: [
    jwtClient(),
    organizationClient(),
    adminClient({
      ac, // Add access controller
      roles: {
        admin: adminRole,
        trainer: trainerRole,
        user: userRole,
      },
    }),
  ],
});
```

### Step 4: Check Permissions

#### Client-Side (Synchronous)
```typescript
// Check if current user's role has permission
const canDeleteUser = authClient.admin.checkRolePermission({
  role: "admin",
  permissions: {
    user: ["delete"]
  }
});

// Check multiple permissions
const canManageWorkouts = authClient.admin.checkRolePermission({
  role: "trainer",
  permissions: {
    workout: ["create", "update", "delete"],
    trainerClient: ["message"]
  }
});
```

#### Server-Side (Async)
```typescript
import { auth } from "@/lib/auth";

// Check user permission
const hasPermission = await auth.api.userHasPermission({
  body: {
    userId: 'user-id',
    permissions: {
      recipe: ["create", "update"]
    }
  }
});

// Check role permission
const roleHasPermission = await auth.api.userHasPermission({
  body: {
    role: "trainer",
    permissions: {
      workout: ["create"],
      trainerClient: ["message"]
    }
  }
});
```

## Admin UI Implementation

### 1. Admin Dashboard Page

Create `frontend/app/admin/page.tsx`:

```typescript
"use client";

import { useSession } from "@/lib/auth-client";
import { redirect } from "next/navigation";

export default function AdminDashboard() {
  const { data: session, isPending } = useSession();

  if (isPending) return <div>Loading...</div>;

  // Check if user is admin
  if (!session?.user || session.user.role !== "admin") {
    redirect("/");
  }

  return (
    <div className="container mx-auto p-6">
      <h1 className="text-3xl font-bold mb-6">Admin Dashboard</h1>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        <AdminCard title="Users" href="/admin/users" />
        <AdminCard title="Roles" href="/admin/roles" />
        <AdminCard title="Sessions" href="/admin/sessions" />
      </div>
    </div>
  );
}
```

### 2. User Management Page

Create `frontend/app/admin/users/page.tsx`:

```typescript
"use client";

import { useState, useEffect } from "react";
import { authClient } from "@/lib/auth-client";

export default function UserManagementPage() {
  const [users, setUsers] = useState([]);
  const [search, setSearch] = useState("");
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    loadUsers();
  }, [search]);

  async function loadUsers() {
    setLoading(true);
    const { data } = await authClient.admin.listUsers({
      searchValue: search,
      searchField: "email",
      searchOperator: "contains",
      limit: 50,
      offset: 0,
      sortBy: "createdAt",
      sortDirection: "desc"
    });

    if (data) {
      setUsers(data.users);
    }
    setLoading(false);
  }

  async function handleBanUser(userId: string) {
    await authClient.admin.banUser({
      userId,
      banReason: "Manual ban by admin",
      banExpiresIn: 60 * 60 * 24 * 7 // 7 days
    });
    loadUsers();
  }

  async function handleUnbanUser(userId: string) {
    await authClient.admin.unbanUser({ userId });
    loadUsers();
  }

  async function handleSetRole(userId: string, role: string) {
    await authClient.admin.setRole({ userId, role });
    loadUsers();
  }

  return (
    <div className="container mx-auto p-6">
      <h1 className="text-3xl font-bold mb-6">User Management</h1>

      <div className="mb-6">
        <input
          type="text"
          placeholder="Search users..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className="w-full px-4 py-2 border rounded"
        />
      </div>

      {loading ? (
        <div>Loading...</div>
      ) : (
        <table className="w-full">
          <thead>
            <tr>
              <th>Email</th>
              <th>Name</th>
              <th>Role</th>
              <th>Status</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {users.map((user) => (
              <tr key={user.id}>
                <td>{user.email}</td>
                <td>{user.name}</td>
                <td>{user.role}</td>
                <td>{user.banned ? "Banned" : "Active"}</td>
                <td>
                  {user.banned ? (
                    <button onClick={() => handleUnbanUser(user.id)}>
                      Unban
                    </button>
                  ) : (
                    <button onClick={() => handleBanUser(user.id)}>
                      Ban
                    </button>
                  )}
                  <select
                    value={user.role}
                    onChange={(e) => handleSetRole(user.id, e.target.value)}
                  >
                    <option value="user">User</option>
                    <option value="trainer">Trainer</option>
                    <option value="admin">Admin</option>
                  </select>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
```

### 3. Create User Form

Create `frontend/app/admin/users/create/page.tsx`:

```typescript
"use client";

import { useState } from "react";
import { authClient } from "@/lib/auth-client";
import { useRouter } from "next/navigation";

export default function CreateUserPage() {
  const router = useRouter();
  const [formData, setFormData] = useState({
    email: "",
    password: "",
    name: "",
    role: "user"
  });

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();

    const { data, error } = await authClient.admin.createUser(formData);

    if (error) {
      alert("Error creating user: " + error.message);
      return;
    }

    alert("User created successfully!");
    router.push("/admin/users");
  }

  return (
    <div className="container mx-auto p-6">
      <h1 className="text-3xl font-bold mb-6">Create New User</h1>

      <form onSubmit={handleSubmit} className="max-w-md">
        <div className="mb-4">
          <label className="block mb-2">Email</label>
          <input
            type="email"
            required
            value={formData.email}
            onChange={(e) => setFormData({ ...formData, email: e.target.value })}
            className="w-full px-4 py-2 border rounded"
          />
        </div>

        <div className="mb-4">
          <label className="block mb-2">Password</label>
          <input
            type="password"
            required
            value={formData.password}
            onChange={(e) => setFormData({ ...formData, password: e.target.value })}
            className="w-full px-4 py-2 border rounded"
          />
        </div>

        <div className="mb-4">
          <label className="block mb-2">Name</label>
          <input
            type="text"
            required
            value={formData.name}
            onChange={(e) => setFormData({ ...formData, name: e.target.value })}
            className="w-full px-4 py-2 border rounded"
          />
        </div>

        <div className="mb-4">
          <label className="block mb-2">Role</label>
          <select
            value={formData.role}
            onChange={(e) => setFormData({ ...formData, role: e.target.value })}
            className="w-full px-4 py-2 border rounded"
          >
            <option value="user">User</option>
            <option value="trainer">Trainer</option>
            <option value="admin">Admin</option>
          </select>
        </div>

        <button type="submit" className="px-6 py-2 bg-blue-600 text-white rounded">
          Create User
        </button>
      </form>
    </div>
  );
}
```

## Security Checklist

### Backend Protection

The BetterAuth admin plugin automatically enforces admin authentication on all admin endpoints. However, you should add additional middleware for Next.js routes:

Create `frontend/middleware.ts`:

```typescript
import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";
import { auth } from "@/lib/auth";

export async function middleware(request: NextRequest) {
  // Protect admin routes
  if (request.nextUrl.pathname.startsWith("/admin")) {
    const session = await auth.api.getSession({
      headers: request.headers
    });

    if (!session?.user || session.user.role !== "admin") {
      return NextResponse.redirect(new URL("/", request.url));
    }
  }

  return NextResponse.next();
}

export const config = {
  matcher: "/admin/:path*"
};
```

### Role Verification

Always verify roles server-side for sensitive operations:

```typescript
// In Server Components or API Routes
import { auth } from "@/lib/auth";
import { headers } from "next/headers";

async function getServerSession() {
  const session = await auth.api.getSession({
    headers: await headers()
  });

  return session;
}

export default async function AdminPage() {
  const session = await getServerSession();

  if (!session?.user || session.user.role !== "admin") {
    redirect("/");
  }

  // Render admin content
}
```

### API Endpoint Protection

All admin endpoints are automatically protected by BetterAuth. They require:
1. Valid authenticated session
2. User has admin role OR user ID in `adminUserIds` array

## Configuration Options Reference

```typescript
admin({
  // Default role assigned to new users
  defaultRole: "user",

  // Which roles are considered admin (when not using custom AC)
  adminRoles: ["admin"],

  // Specific user IDs that always have admin access
  adminUserIds: ["user-uuid-1", "user-uuid-2"],

  // How long impersonation sessions last (in seconds)
  impersonationSessionDuration: 60 * 60 * 24, // 24 hours

  // Allow admins to impersonate other admins
  allowImpersonatingAdmins: false,

  // Custom function to determine if user is admin
  async isAdmin(user) {
    return user.role === "admin" || user.email.endsWith("@company.com");
  },

  // Access control (optional)
  ac: accessController,
  roles: {
    admin: adminRole,
    user: userRole,
    // ... custom roles
  }
})
```

## API Endpoints Summary

| Endpoint | Method | Description | Auth Required |
|----------|--------|-------------|---------------|
| `/admin/create-user` | POST | Create new user | Admin |
| `/admin/update-user` | POST | Update user details | Admin |
| `/admin/set-user-password` | POST | Change user password | Admin |
| `/admin/set-role` | POST | Change user role | Admin |
| `/admin/ban-user` | POST | Ban user | Admin |
| `/admin/unban-user` | POST | Unban user | Admin |
| `/admin/revoke-user-session` | POST | Revoke specific session | Admin |
| `/admin/revoke-user-sessions` | POST | Revoke all user sessions | Admin |
| `/admin/impersonate-user` | POST | Start impersonating user | Admin |
| `/admin/stop-impersonating` | POST | Stop impersonation | Admin |
| `/admin/list-users` | GET | List/search users | Admin |
| `/admin/has-permission` | POST | Check permissions | Admin |

## Migration Steps

### 1. Database Migration (Required)

```bash
cd frontend
bun run db:generate
bun run db:migrate
```

Verify these fields exist in `users` table:
- `role` (varchar)
- `banned` (boolean)
- `banReason` (text)
- `banExpires` (timestamp)

Verify this field exists in `sessions` table:
- `impersonatedBy` (uuid)

### 2. Custom Access Control (Optional)

If implementing custom permissions:

1. Create `frontend/lib/permissions.ts` (see above)
2. Update `frontend/lib/auth.ts` server config
3. Update `frontend/lib/auth-client.ts` client config
4. Restart dev server

### 3. Create Admin UI (Recommended)

1. Create admin pages under `frontend/app/admin/`
2. Add route protection middleware
3. Implement user management UI
4. Add permission checks in components

## Testing Checklist

- [ ] Run database migration
- [ ] Verify admin plugin loads without errors
- [ ] Create test admin user manually in database
- [ ] Test admin login
- [ ] Test creating new user via API
- [ ] Test banning/unbanning user
- [ ] Test role assignment
- [ ] Test session revocation
- [ ] Test user impersonation
- [ ] Test permission checks (if using custom AC)
- [ ] Test admin UI routes are protected
- [ ] Test non-admin users cannot access admin features

## Best Practices

1. **Never hardcode admin credentials** - Use environment variables or create first admin via database
2. **Always verify roles server-side** - Client-side checks are for UX only
3. **Log admin actions** - Implement audit logging for user management operations
4. **Use impersonation sparingly** - Only for debugging/support scenarios
5. **Set reasonable session durations** - Balance security vs convenience
6. **Regular access reviews** - Periodically audit who has admin access
7. **Principle of least privilege** - Use custom access control for fine-grained permissions

## Troubleshooting

### "User not authorized" errors
- Verify user has `role: "admin"` in database
- Check `adminRoles` config matches user's role
- Ensure session is valid and not expired

### Migration failures
- Check database connection
- Verify Drizzle config is correct
- Run `bun run db:push` as alternative
- Manually add fields if needed

### Impersonation not working
- Verify `impersonatedBy` field exists in sessions table
- Check `impersonationSessionDuration` is set
- Ensure admin user is authenticated

### Custom permissions not enforced
- Verify `ac` and `roles` are passed to both server and client configs
- Check permission definitions match exactly
- Ensure roles are assigned correctly in database

## Next Steps

1. Run database migration
2. Test basic admin functionality (create user, ban, etc.)
3. Decide if custom access control is needed
4. Implement admin UI pages
5. Add audit logging for admin actions
6. Document admin procedures for team

## References

- [BetterAuth Admin Plugin Docs](https://www.better-auth.com/docs/plugins/admin)
- [BetterAuth Access Control](https://www.better-auth.com/docs/plugins/access)
- Project CLAUDE.md for architectural guidelines
