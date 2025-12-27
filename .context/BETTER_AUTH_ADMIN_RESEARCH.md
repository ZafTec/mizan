# Better Auth Admin Implementation - Comprehensive Research

## Overview

Better Auth provides a comprehensive admin plugin system with role-based access control (RBAC), user management, and permission systems. This document consolidates research from official Better Auth documentation.

---

## 1. Core Admin Plugin Setup

### Server-Side Configuration

```typescript
import { betterAuth } from "better-auth"
import { admin } from "better-auth/plugins"

export const auth = betterAuth({
    plugins: [
        admin({
            // Optional: Custom admin check logic
            async isAdmin(user) {
                return user.role === "admin" || user.email.endsWith("@company.com");
            },

            // Optional: Specify admin user IDs
            adminUserIds: ["user_id_1", "user_id_2"],

            // Optional: Define which roles are admin (when not using custom AC)
            adminRoles: ["admin", "superadmin"],

            // Optional: Default role for new users
            defaultRole: "user",

            // Optional: Custom ban message
            bannedUserMessage: "Your account has been suspended. Contact support.",

            // Optional: Allow admins to impersonate other admins
            allowImpersonatingAdmins: false, // default: false
        })
    ]
})
```

### Client-Side Configuration

```typescript
import { createAuthClient } from "better-auth/client"
import { adminClient } from "better-auth/client/plugins"

export const authClient = createAuthClient({
    plugins: [
        adminClient()
    ]
})
```

### Database Migration

After adding the admin plugin, migrate your database:

```bash
# Using Better Auth CLI
npx better-auth migrate

# Or generate schema for your ORM
npx better-auth generate
```

---

## 2. Role-Based Access Control (RBAC)

### Default Permissions

The admin plugin includes two default resources:

**User Resource:**
- `create` - Create new users
- `list` - List all users
- `set-role` - Change user roles
- `ban` - Ban/unban users
- `impersonate` - Impersonate users
- `delete` - Delete users
- `set-password` - Set user passwords

**Session Resource:**
- `list` - List user sessions
- `revoke` - Revoke sessions
- `delete` - Delete sessions

**Default Roles:**
- `admin` - Full control over all resources and actions
- `user` - No administrative control

### Creating Custom Access Control

#### Step 1: Define Statement (Resources & Actions)

```typescript
// lib/auth/permissions.ts
import { createAccessControl } from "better-auth/plugins/access";

// Define resources and their allowed actions
const statement = {
    project: ["create", "share", "update", "delete"],
    user: ["ban", "suspend"],
    reports: ["view", "export", "delete"],
} as const; // ⚠️ IMPORTANT: Use 'as const' for type inference

export const ac = createAccessControl(statement);
```

#### Step 2: Define Custom Roles

```typescript
// lib/auth/permissions.ts (continued)

export const user = ac.newRole({
    project: ["create"], // Users can only create projects
});

export const moderator = ac.newRole({
    project: ["create", "update"],
    user: ["suspend"], // Can suspend users but not ban
});

export const admin = ac.newRole({
    project: ["create", "update", "delete"],
    user: ["ban", "suspend"],
    reports: ["view", "export"],
});

export const superadmin = ac.newRole({
    project: ["create", "share", "update", "delete"],
    user: ["ban", "suspend"],
    reports: ["view", "export", "delete"],
});
```

#### Step 3: Extend Default Admin Permissions

```typescript
import { createAccessControl } from "better-auth/plugins/access";
import { defaultStatements, adminAc } from "better-auth/plugins/admin/access";

// Merge default admin statements with custom ones
const statement = {
    ...defaultStatements, // Includes user & session resources
    project: ["create", "share", "update", "delete"],
} as const;

const ac = createAccessControl(statement);

// Admin role with both default and custom permissions
export const admin = ac.newRole({
    project: ["create", "update"],
    ...adminAc.statements // Includes all default admin permissions
});
```

#### Step 4: Integrate with Server Plugin

```typescript
import { betterAuth } from "better-auth";
import { admin as adminPlugin } from "better-auth/plugins";
import { ac, admin, moderator, user, superadmin } from "@/lib/auth/permissions";

export const auth = betterAuth({
    plugins: [
        adminPlugin({
            ac,
            roles: {
                admin,
                moderator,
                user,
                superadmin
            }
        })
    ]
});
```

#### Step 5: Integrate with Client Plugin

```typescript
import { createAuthClient } from "better-auth/client";
import { adminClient } from "better-auth/client/plugins";
import { ac, admin, moderator, user, superadmin } from "@/lib/auth/permissions";

export const authClient = createAuthClient({
    plugins: [
        adminClient({
            ac,
            roles: {
                admin,
                moderator,
                user,
                superadmin
            }
        })
    ]
});
```

---

## 3. Admin Operations - API Reference

### User Management

#### Create User

```typescript
const { data: newUser } = await authClient.admin.createUser({
    email: "user@example.com",
    password: "secure-password",
    name: "James Smith",
    role: "user", // or ["user", "moderator"] for multiple roles
    data: {
        customField: "customValue" // Additional fields
    }
});
```

#### List Users (with Pagination, Search, Filter, Sort)

```typescript
const { data: result } = await authClient.admin.listUsers({
    query: {
        // Pagination
        limit: 50,
        offset: 0,

        // Search
        searchValue: "john",
        searchField: "name", // or "email"
        searchOperator: "contains", // or "starts_with", "ends_with"

        // Filter
        filterField: "role",
        filterValue: "admin",
        filterOperator: "eq", // or "ne", "lt", "lte", "gt", "gte"

        // Sort
        sortBy: "createdAt",
        sortDirection: "desc", // or "asc"
    }
});

// Response structure
const users = result.users; // User[]
const total = result.total; // number
const limit = result.limit; // number | undefined
const offset = result.offset; // number | undefined

// Calculate pagination
const pageSize = 10;
const currentPage = 2;
const totalPages = Math.ceil(result.total / pageSize);
```

#### Ban/Unban Users

```typescript
// Ban user (permanently or temporarily)
await authClient.admin.banUser({
    userId: "user-id",
    banReason: "Violation of terms",
    banExpiresIn: 60 * 60 * 24 * 7 // 7 days in seconds (optional)
});

// Unban user
await authClient.admin.unbanUser({
    userId: "user-id"
});
```

#### Set User Role

```typescript
// Single role
await authClient.admin.setRole({
    userId: "user-id",
    role: "admin"
});

// Multiple roles
await authClient.admin.setRole({
    userId: "user-id",
    role: ["admin", "moderator"]
});
```

#### User Impersonation

```typescript
// Start impersonating
await authClient.admin.impersonateUser({
    userId: "user_123"
});

// Stop impersonating (return to admin account)
await authClient.admin.stopImpersonating();
```

#### Session Management

```typescript
// List all sessions for a user
const sessions = await authClient.admin.listUserSessions({
    userId: "user-id"
});

// Sessions include: id, ipAddress, userAgent, expiresAt, etc.
```

### Permission Checking

#### Server-Side Permission Check

```typescript
import { auth } from "@/lib/auth";
import { headers } from "next/headers";

// Check single permission
const hasPermission = await auth.api.hasPermission({
    headers: await headers(),
    body: {
        permissions: {
            project: ["create"]
        }
    }
});

// Check multiple permissions
const hasMultiplePermissions = await auth.api.hasPermission({
    headers: await headers(),
    body: {
        permissions: {
            project: ["create"],
            reports: ["export"]
        }
    }
});

// Check by role (instead of user from session)
const roleHasPermission = await auth.api.hasPermission({
    headers: await headers(),
    body: {
        role: "moderator",
        permissions: {
            user: ["ban"]
        }
    }
});
```

---

## 4. Middleware and Route Protection

### Next.js Middleware (Optimistic Cookie Check)

```typescript
// middleware.ts
import { NextRequest, NextResponse } from "next/server";
import { getSessionCookie } from "better-auth/cookies";

export async function middleware(request: NextRequest) {
    const sessionCookie = getSessionCookie(request);
    const { pathname } = request.nextUrl;

    // Redirect authenticated users away from auth pages
    if (sessionCookie && ["/login", "/signup"].includes(pathname)) {
        return NextResponse.redirect(new URL("/dashboard", request.url));
    }

    // Redirect unauthenticated users to login
    if (!sessionCookie && pathname.startsWith("/dashboard")) {
        return NextResponse.redirect(new URL("/login", request.url));
    }

    return NextResponse.next();
}

export const config = {
    matcher: ["/dashboard/:path*", "/login", "/signup"],
};
```

**⚠️ IMPORTANT:** Cookie-based checks are NOT secure. They're optimistic redirects only. Always validate on the server.

### Next.js Middleware (Full Session Validation - Next.js 15.2+)

```typescript
// middleware.ts
import { NextRequest, NextResponse } from "next/server";
import { headers } from "next/headers";
import { auth } from "@/lib/auth";

export async function middleware(request: NextRequest) {
    const session = await auth.api.getSession({
        headers: await headers()
    });

    if (!session && request.nextUrl.pathname.startsWith("/dashboard")) {
        return NextResponse.redirect(new URL("/login", request.url));
    }

    return NextResponse.next();
}

export const config = {
    runtime: "nodejs", // Required for database access
    matcher: ["/dashboard/:path*"]
};
```

### Server Component Protection

```typescript
// app/dashboard/page.tsx
import { auth } from "@/lib/auth";
import { headers } from "next/headers";
import { redirect } from "next/navigation";

export default async function DashboardPage() {
    const session = await auth.api.getSession({
        headers: await headers()
    });

    if (!session) {
        redirect("/login");
    }

    // Optional: Check if user is admin
    const isAdmin = session.user.role === "admin" ||
                    session.user.role?.includes("admin");

    return (
        <div>
            <h1>Welcome, {session.user.name}!</h1>
            {isAdmin && <AdminPanel />}
        </div>
    );
}
```

### API Route Protection

```typescript
// app/api/admin/users/route.ts
import { auth } from "@/lib/auth";
import { NextRequest } from "next/server";

export async function GET(req: NextRequest) {
    const session = await auth.api.getSession({
        headers: req.headers
    });

    if (!session) {
        return new Response("Unauthorized", { status: 401 });
    }

    // Check admin permission
    const hasPermission = await auth.api.hasPermission({
        headers: req.headers,
        body: {
            permissions: {
                user: ["list"]
            }
        }
    });

    if (!hasPermission) {
        return new Response("Forbidden", { status: 403 });
    }

    // Proceed with admin operation
    const users = await fetchUsers();
    return Response.json({ users });
}
```

---

## 5. Client-Side Session Management

### useSession Hook

```typescript
"use client";

import { useSession } from "@/lib/auth-client";

export function ProfilePage() {
    const { data: session, isPending, error } = useSession();

    if (isPending) {
        return <Loader />;
    }

    if (error) {
        return <div>Error: {error.message}</div>;
    }

    if (!session) {
        return <div>Not authenticated</div>;
    }

    return (
        <div>
            <h1>Welcome, {session.user.name}!</h1>
            <p>Email: {session.user.email}</p>
            <p>Role: {session.user.role}</p>

            <div>
                <h2>Session Info</h2>
                <p>Session ID: {session.session.id}</p>
                <p>Expires: {new Date(session.session.expiresAt).toLocaleString()}</p>
                <p>IP: {session.session.ipAddress}</p>
                <p>User Agent: {session.session.userAgent}</p>
            </div>
        </div>
    );
}
```

### Profile Management

```typescript
import { authClient } from "@/lib/auth-client";

// Update user profile
const { data, error } = await authClient.updateUser({
    name: "John Smith",
    image: "https://example.com/avatar.jpg"
});

// Change email
await authClient.changeEmail({
    newEmail: "newemail@example.com",
    callbackURL: "/verify-new-email"
});

// Change password
await authClient.changePassword({
    currentPassword: "OldPassword123!",
    newPassword: "NewPassword456!",
    revokeOtherSessions: true // Sign out other devices
});
```

---

## 6. Best Practices

### Security

1. **Never use cookie-based checks for authorization** - Only for optimistic UI/redirects
2. **Always validate on the server** - Use `auth.api.getSession()` in server components/API routes
3. **Check permissions explicitly** - Don't rely on role names alone, use permission checks
4. **Principle of least privilege** - Grant minimum required permissions
5. **Audit admin actions** - Log who did what and when

### Role Design

1. **Keep roles focused** - Each role should have a clear purpose
2. **Use composition** - Users can have multiple roles
3. **Separate concerns** - Different resources for different domains (user management vs. content management)
4. **Document permissions** - Clearly document what each permission allows

### User Management

1. **Provide ban reasons** - Always include a reason when banning users
2. **Use temporary bans** - Consider time-limited bans for first offenses
3. **Impersonation safety** - Disable admin-to-admin impersonation by default
4. **Session management** - Allow admins to view and revoke user sessions

### Database

1. **Don't store sensitive data in user/session tables** - Create dedicated tables
2. **Run migrations** - Always migrate after adding admin plugin
3. **Use transactions** - For operations affecting multiple users/roles

### Client-Side

1. **Handle loading states** - Show spinners while checking permissions
2. **Graceful degradation** - Hide admin features if user lacks permissions
3. **Error handling** - Show appropriate messages for permission errors
4. **Optimistic UI** - Update UI immediately, revert on error

---

## 7. Common Patterns

### Admin Panel Layout

```typescript
// app/admin/layout.tsx
import { auth } from "@/lib/auth";
import { headers } from "next/headers";
import { redirect } from "next/navigation";

export default async function AdminLayout({
    children
}: {
    children: React.ReactNode
}) {
    const session = await auth.api.getSession({
        headers: await headers()
    });

    if (!session) {
        redirect("/login");
    }

    // Check if user has admin role
    const isAdmin = session.user.role === "admin" ||
                    session.user.role?.includes("admin");

    if (!isAdmin) {
        redirect("/dashboard"); // Or show 403 page
    }

    return (
        <div className="admin-layout">
            <AdminSidebar user={session.user} />
            <main>{children}</main>
        </div>
    );
}
```

### Permission-Based Component Rendering

```typescript
"use client";

import { useSession } from "@/lib/auth-client";

export function AdminActions({ userId }: { userId: string }) {
    const { data: session } = useSession();

    // Check if user has admin role (client-side check for UI only)
    const isAdmin = session?.user.role === "admin";

    if (!isAdmin) return null;

    return (
        <div className="admin-actions">
            <button onClick={() => handleBanUser(userId)}>Ban User</button>
            <button onClick={() => handleImpersonate(userId)}>Impersonate</button>
        </div>
    );
}
```

### Paginated User List

```typescript
"use client";

import { useState } from "react";
import { authClient } from "@/lib/auth-client";

export function UserList() {
    const [page, setPage] = useState(1);
    const [search, setSearch] = useState("");
    const pageSize = 20;

    const { data, isLoading } = useQuery({
        queryKey: ["users", page, search],
        queryFn: async () => {
            const result = await authClient.admin.listUsers({
                query: {
                    limit: pageSize,
                    offset: (page - 1) * pageSize,
                    searchValue: search,
                    searchField: "name",
                    searchOperator: "contains",
                    sortBy: "createdAt",
                    sortDirection: "desc"
                }
            });
            return result.data;
        }
    });

    const totalPages = data ? Math.ceil(data.total / pageSize) : 0;

    return (
        <div>
            <input
                type="text"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="Search users..."
            />

            {isLoading ? (
                <Spinner />
            ) : (
                <>
                    <UserTable users={data.users} />
                    <Pagination
                        current={page}
                        total={totalPages}
                        onChange={setPage}
                    />
                </>
            )}
        </div>
    );
}
```

---

## 8. Migration Notes

### Adding Admin to Existing Project

1. Add admin plugin to server config
2. Add adminClient to client config
3. Run database migration: `npx better-auth migrate`
4. Update existing user roles in database if needed
5. Add permission checks to protected routes
6. Build admin UI components

### Schema Changes

The admin plugin adds these fields to the `user` table:
- `role` (string) - Stores roles as comma-separated values
- `banned` (boolean) - Whether user is banned
- `banReason` (string, nullable) - Reason for ban
- `banExpires` (timestamp, nullable) - When ban expires

---

## 9. Troubleshooting

### Common Issues

**Issue:** "User not authorized" despite having admin role

**Solution:** Check if:
- Admin plugin is configured on both server and client
- Database migration was run
- User role field is set correctly in database
- Session is being passed correctly in headers

**Issue:** Permission checks always return false

**Solution:** Verify:
- Access control (`ac`) is configured correctly
- Roles are defined with correct permissions
- Custom permissions match your statement definition
- Both server and client have same `ac` and `roles` config

**Issue:** Can't impersonate users

**Solution:** Check:
- Admin has `user` resource with `impersonate` permission
- `allowImpersonatingAdmins` is enabled if impersonating admins
- User is authenticated as admin before attempting impersonation

---

## 10. Resources

- [Better Auth Admin Plugin Docs](https://github.com/better-auth/better-auth/blob/canary/docs/content/docs/plugins/admin.mdx)
- [Better Auth Access Control](https://github.com/better-auth/better-auth/blob/canary/docs/content/docs/plugins/access.mdx)
- [Better Auth Organization Plugin](https://github.com/better-auth/better-auth/blob/canary/docs/content/docs/plugins/organization.mdx) (for multi-tenant RBAC)
- [Better Auth Session Management](https://github.com/better-auth/better-auth/blob/canary/docs/content/docs/concepts/session-management.mdx)

---

## Summary

Better Auth provides a comprehensive admin system with:

✅ **Built-in user management** - Create, list, ban, impersonate users
✅ **Flexible RBAC** - Custom roles and permissions with type safety
✅ **Multiple roles per user** - Users can have multiple roles
✅ **Fine-grained permissions** - Resource-action based permission model
✅ **Server-side validation** - Secure session and permission checks
✅ **Client-side hooks** - React hooks for session management
✅ **Middleware support** - Route protection for Next.js and other frameworks
✅ **Session management** - View and revoke user sessions
✅ **Custom admin logic** - Flexible `isAdmin` function
✅ **TypeScript support** - Full type inference with `as const`

The system is production-ready, type-safe, and designed for real-world applications with complex permission requirements.
