# Access Control Design: Trainer-Client Relationships

## Overview

This document defines the access control model for trainer (coach) and client (user) interactions in the macro_chef application. The design follows RBAC (Role-Based Access Control) principles aligned with healthcare fitness app best practices and privacy regulations.

## Regulatory Context

- **GDPR** (Europe): User consent required for data sharing
- **CCPA** (California): Right to know, delete, opt-out
- **HIPAA-aligned** (US Healthcare): Auditable access, minimum necessary principle
- **General Fitness Apps**: Transparent privacy policies, encrypted data, incident response

## Roles

**IMPORTANT**: System admin and trainer are completely separate roles.

| Role | Description | Capabilities |
|------|-------------|--------------|
| `user` | Regular application user | Manage own data, request trainers |
| `trainer` | Certified fitness coach | View assigned clients' data (with consent), provide guidance. **NOT a system admin** |
| `admin` | System administrator | Full access to admin panel, user management, system configuration. **NOT a trainer** |

A user can be **both** `trainer` AND assigned client management permissions through the trainer-client relationship system, but only `admin` role has access to system administration features.

## Resources and Permissions

### Client Resources Accessible by Trainer

| Resource | Permissions | Description |
|----------|-------------|-------------|
| **Nutrition Data** | `view`, `comment` | Meal plans, recipes, daily intake, macros |
| **Workout Data** | `view`, `comment`, `assign` | Exercise logs, workout plans, performance metrics |
| **Body Measurements** | `view`, `comment` | Weight, body composition, progress photos |
| **Goals** | `view`, `edit` | Target weight, macro goals, fitness objectives |
| **Profile** | `view` | Name, email, basic info (not password/sessions) |
| **Messaging** | `send`, `receive` | In-app chat communication |

### Permission Matrix

| Action | User (Own Data) | Trainer (Client Data) | Admin |
|--------|----------------|----------------------|-------|
| View nutrition | ✅ | ✅ (with consent) | ✅ |
| Edit nutrition | ✅ | ❌ | ✅ |
| View workouts | ✅ | ✅ (with consent) | ✅ |
| Assign workouts | ❌ | ✅ (with consent) | ✅ |
| View measurements | ✅ | ✅ (with consent) | ✅ |
| Edit measurements | ✅ | ❌ | ✅ |
| View goals | ✅ | ✅ (with consent) | ✅ |
| Edit goals | ✅ | ✅ (with consent) | ✅ |
| Message client | N/A | ✅ (with consent) | ✅ |

## Consent Management

### Consent Model

```typescript
interface TrainerClientConsent {
  id: string;
  clientId: string;
  trainerId: string;
  status: 'pending' | 'active' | 'revoked' | 'expired';
  permissions: {
    viewNutrition: boolean;
    commentNutrition: boolean;
    viewWorkouts: boolean;
    commentWorkouts: boolean;
    assignWorkouts: boolean;
    viewMeasurements: boolean;
    commentMeasurements: boolean;
    viewGoals: boolean;
    editGoals: boolean;
    messaging: boolean;
  };
  consentedAt: Date | null;
  revokedAt: Date | null;
  expiresAt: Date | null;
  auditLog: ConsentAuditEntry[];
}
```

### Consent Workflow

1. **Client Requests Trainer**:
   - Client searches for trainers via SignalR coach search
   - Client sends request with desired permissions
   - Status: `pending`

2. **Trainer Accepts**:
   - Trainer reviews request and permissions
   - Trainer accepts → status changes to `active`
   - `consentedAt` timestamp recorded

3. **Active Relationship**:
   - Trainer can access client data per granted permissions
   - All access logged to audit trail
   - Client can view access logs

4. **Revocation**:
   - Client can revoke consent anytime
   - Status changes to `revoked`
   - `revokedAt` timestamp recorded
   - Trainer loses immediate access

5. **Expiration** (Optional):
   - Consent can have expiration date
   - Status auto-changes to `expired`
   - Requires renewal for continued access

## Implementation with Better Auth

### Database Schema Extension

```typescript
// frontend/db/schema.ts additions

export const trainerClientRelationships = pgTable("trainer_client_relationships", {
  id: uuid("id").defaultRandom().primaryKey(),
  clientId: uuid("client_id").references(() => users.id, { onDelete: "cascade" }).notNull(),
  trainerId: uuid("trainer_id").references(() => users.id, { onDelete: "cascade" }).notNull(),
  status: varchar("status", { length: 20 }).notNull().default("pending"), // pending, active, revoked, expired

  // Granular permissions
  viewNutrition: boolean("view_nutrition").default(false),
  commentNutrition: boolean("comment_nutrition").default(false),
  viewWorkouts: boolean("view_workouts").default(false),
  commentWorkouts: boolean("comment_workouts").default(false),
  assignWorkouts: boolean("assign_workouts").default(false),
  viewMeasurements: boolean("view_measurements").default(false),
  commentMeasurements: boolean("comment_measurements").default(false),
  viewGoals: boolean("view_goals").default(false),
  editGoals: boolean("edit_goals").default(false),
  messaging: boolean("messaging").default(true),

  // Timestamps
  requestedAt: timestamp("requested_at", { withTimezone: true }).defaultNow().notNull(),
  consentedAt: timestamp("consented_at", { withTimezone: true }),
  revokedAt: timestamp("revoked_at", { withTimezone: true }),
  expiresAt: timestamp("expires_at", { withTimezone: true }),

  createdAt: timestamp("created_at", { withTimezone: true }).defaultNow().notNull(),
  updatedAt: timestamp("updated_at", { withTimezone: true }).defaultNow().notNull(),
});

export const accessAuditLog = pgTable("access_audit_log", {
  id: uuid("id").defaultRandom().primaryKey(),
  relationshipId: uuid("relationship_id").references(() => trainerClientRelationships.id, { onDelete: "cascade" }).notNull(),
  trainerId: uuid("trainer_id").references(() => users.id, { onDelete: "cascade" }).notNull(),
  clientId: uuid("client_id").references(() => users.id, { onDelete: "cascade" }).notNull(),

  action: varchar("action", { length: 100 }).notNull(), // e.g., "viewed_nutrition", "assigned_workout"
  resource: varchar("resource", { length: 100 }).notNull(), // e.g., "meal_plan", "workout_log"
  resourceId: uuid("resource_id"), // ID of the specific resource accessed

  ipAddress: text("ip_address"),
  userAgent: text("user_agent"),
  timestamp: timestamp("timestamp", { withTimezone: true }).defaultNow().notNull(),
});
```

### Access Control Middleware

```typescript
// frontend/lib/access-control.ts

import { auth } from "@/lib/auth";
import { db } from "@/db/db";
import { trainerClientRelationships, accessAuditLog } from "@/db/schema";
import { eq, and } from "drizzle-orm";

export interface AccessContext {
  userId: string;
  userRole: string;
  targetUserId?: string;
}

export type Permission =
  | "viewNutrition"
  | "commentNutrition"
  | "viewWorkouts"
  | "commentWorkouts"
  | "assignWorkouts"
  | "viewMeasurements"
  | "commentMeasurements"
  | "viewGoals"
  | "editGoals"
  | "messaging";

export async function checkAccess(
  context: AccessContext,
  permission: Permission
): Promise<boolean> {
  const { userId, userRole, targetUserId } = context;

  // Admin always has access
  if (userRole === "admin") {
    return true;
  }

  // User accessing their own data
  if (userId === targetUserId || !targetUserId) {
    return true;
  }

  // Trainer accessing client data
  if (userRole === "trainer" && targetUserId) {
    const relationship = await db
      .select()
      .from(trainerClientRelationships)
      .where(
        and(
          eq(trainerClientRelationships.trainerId, userId),
          eq(trainerClientRelationships.clientId, targetUserId),
          eq(trainerClientRelationships.status, "active")
        )
      )
      .limit(1);

    if (relationship.length === 0) {
      return false;
    }

    return relationship[0][permission] === true;
  }

  return false;
}

export async function logAccess(
  relationshipId: string,
  trainerId: string,
  clientId: string,
  action: string,
  resource: string,
  resourceId?: string,
  metadata?: { ipAddress?: string; userAgent?: string }
): Promise<void> {
  await db.insert(accessAuditLog).values({
    relationshipId,
    trainerId,
    clientId,
    action,
    resource,
    resourceId,
    ipAddress: metadata?.ipAddress,
    userAgent: metadata?.userAgent,
  });
}

export async function requireAccess(
  context: AccessContext,
  permission: Permission
): Promise<void> {
  const hasAccess = await checkAccess(context, permission);
  if (!hasAccess) {
    throw new Error(`Access denied: ${permission} not granted`);
  }
}
```

### Usage in API Routes

```typescript
// Example: frontend/app/api/nutrition/[userId]/route.ts

import { auth } from "@/lib/auth";
import { checkAccess, logAccess } from "@/lib/access-control";

export async function GET(
  request: Request,
  { params }: { params: { userId: string } }
) {
  const session = await auth.api.getSession({ headers: request.headers });

  if (!session) {
    return Response.json({ error: "Unauthorized" }, { status: 401 });
  }

  const hasAccess = await checkAccess(
    {
      userId: session.user.id,
      userRole: session.user.role,
      targetUserId: params.userId,
    },
    "viewNutrition"
  );

  if (!hasAccess) {
    return Response.json({ error: "Access denied" }, { status: 403 });
  }

  // If trainer accessing client data, log it
  if (session.user.role === "trainer" && session.user.id !== params.userId) {
    const relationship = await db
      .select({ id: trainerClientRelationships.id })
      .from(trainerClientRelationships)
      .where(
        and(
          eq(trainerClientRelationships.trainerId, session.user.id),
          eq(trainerClientRelationships.clientId, params.userId),
          eq(trainerClientRelationships.status, "active")
        )
      )
      .limit(1);

    if (relationship.length > 0) {
      await logAccess(
        relationship[0].id,
        session.user.id,
        params.userId,
        "viewed_nutrition",
        "meal_plan",
        undefined,
        {
          ipAddress: request.headers.get("x-forwarded-for") || undefined,
          userAgent: request.headers.get("user-agent") || undefined,
        }
      );
    }
  }

  // Fetch and return nutrition data
  const nutritionData = await fetchNutritionData(params.userId);
  return Response.json(nutritionData);
}
```

## Security Best Practices

### 1. Principle of Least Privilege
- Clients grant minimum permissions necessary
- Default permissions: `messaging: true`, all others `false`
- Explicit opt-in for data sharing

### 2. Audit Trail
- All trainer access to client data logged
- Includes: timestamp, action, resource, IP, user agent
- Clients can review access logs
- Logs retained for compliance (90 days minimum)

### 3. Consent Transparency
- Clear UI showing what permissions grant access to
- Real-time notification when trainer accesses data (optional)
- Easy revocation process

### 4. Session-Based Access
- Access tied to active trainer-client relationship
- Revocation immediately invalidates access
- No cached data after revocation

### 5. Data Encryption
- All data encrypted at rest (PostgreSQL TDE)
- All data encrypted in transit (HTTPS/WSS)
- JWT tokens for authentication

### 6. Incident Response
- Automated alerts for suspicious access patterns
- Bulk access attempts flagged
- Admin can force-revoke relationships

## UI/UX Flow

### Client Perspective

1. **Request Trainer**:
   - Search trainers via `/trainer` page
   - View trainer profile (specialties, rating, clients)
   - Click "Request as My Coach"
   - Modal: Select permissions to grant
   - Submit request

2. **Manage Trainer**:
   - View active trainer relationship in `/profile/trainers`
   - See granted permissions
   - View access log (recent trainer actions)
   - Revoke access anytime

3. **Access Notifications** (Optional):
   - Real-time toast: "Your trainer viewed your meal plan"
   - Weekly summary email: "Your trainer accessed: 5 meal plans, 3 workout logs"

### Trainer Perspective

1. **Accept Client**:
   - View pending requests in `/trainer/requests`
   - Review requested permissions
   - Accept or decline

2. **Manage Clients**:
   - View active clients in `/trainer/clients`
   - Click client → see granted permissions
   - Access client data per permissions
   - Request additional permissions (client must approve)

3. **Data Access**:
   - All client data views show permission badge
   - Disabled actions shown grayed out with tooltip: "Permission not granted"

### Admin Perspective

1. **Monitor Relationships**:
   - View all trainer-client relationships in `/admin/relationships`
   - Filter by status, trainer, client
   - View audit logs

2. **Intervene**:
   - Force-revoke relationship (abuse cases)
   - Ban trainer/client
   - Export audit logs for compliance

## Migration Plan

### Phase 1: Database Schema
- [ ] Add `trainerClientRelationships` table
- [ ] Add `accessAuditLog` table
- [ ] Generate and run Drizzle migration

### Phase 2: Access Control Library
- [ ] Implement `lib/access-control.ts`
- [ ] Add middleware to existing API routes
- [ ] Add audit logging

### Phase 3: UI Implementation
- [ ] Client: Request trainer flow
- [ ] Client: Manage permissions UI
- [ ] Trainer: Accept requests flow
- [ ] Trainer: View client data with permission checks
- [ ] Admin: Relationship management UI

### Phase 4: Testing
- [ ] Unit tests for `checkAccess()`
- [ ] Integration tests for API routes
- [ ] E2E tests for full flow

### Phase 5: Documentation
- [ ] User-facing privacy policy
- [ ] Trainer guidelines
- [ ] Admin moderation guide

## References

- [Microsoft RBAC Implementation Guide](https://learn.microsoft.com/en-us/entra/identity-platform/howto-implement-rbac-for-apps)
- [RBAC for Healthcare SaaS](https://www.cabotsolutions.com/blog/role-based-access-control-rbac-for-secure-healthcare-saas-applications)
- [Fitness Trainer Data Security](https://www.newsoftwares.net/blog/how-should-a-personal-trainer-document-aand-secure-client-data/)
- [Better Auth Access Control](https://www.better-auth.com/docs/concepts/access-control)
