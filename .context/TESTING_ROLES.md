# Role-Based Access Control Testing Guide

**Date**: 2025-12-27
**Status**: Active
**Purpose**: Comprehensive guide for testing user roles (user, trainer, admin) in MacroChef

## Overview

MacroChef implements three distinct user roles with different permissions and access levels:

| Role | Description | Primary Features |
|------|-------------|------------------|
| `user` | Regular users | Meal planning, nutrition tracking, workouts, household management |
| `trainer` | Fitness professionals | Client management, workout assignment, messaging, client data viewing |
| `admin` | System administrators | Full system access, user management, impersonation, system configuration |

## Quick Start: Creating Test Users

### Option 1: Admin UI (Recommended)

**Prerequisites**: You must have at least one admin user in the database (see SQL method below for bootstrap).

1. Start the application:
   ```bash
   docker-compose up -d
   ```

2. Log in as an admin user

3. Navigate to Admin Dashboard:
   ```
   http://localhost:3000/admin
   ```

4. Click "User Management" or navigate to:
   ```
   http://localhost:3000/admin/users
   ```

5. Click "Create User" button

6. Fill in the form:
   - Email: `test-user@example.com`
   - Full Name: `Test User`
   - Role: Select from dropdown (user, trainer, admin)
   - Password: Minimum 8 characters
   - Confirm Password: Must match

7. Click "Create User"

The user is created immediately with the specified role. No email verification required.

### Option 2: SQL Commands (Bootstrap)

Use this method to create the **first admin user** when no admin exists yet.

**Connect to PostgreSQL**:
```bash
# Using Docker
docker exec -it mizan-postgres psql -U mizan -d mizan

# Or using local psql client
psql -h localhost -U mizan -d mizan
```

**Create Admin User**:
```sql
-- 1. Create the user account with hashed password
-- Password: "admin123" (use better-auth hashing in production)
INSERT INTO users (id, email, email_verified, name, role, created_at, updated_at)
VALUES (
  gen_random_uuid(),
  'admin@macrochef.local',
  true,
  'System Admin',
  'admin',
  NOW(),
  NOW()
)
RETURNING id;

-- 2. Note the returned ID, then create the password account
-- You'll need to use better-auth's password hashing
-- For testing, you can use the signup endpoint instead:
-- POST http://localhost:3000/api/auth/sign-up
-- Then manually update the role to 'admin'

-- 3. Quick way: Create via signup, then update role
-- First, sign up normally through UI with:
-- Email: admin@macrochef.local
-- Password: Admin123!
-- Then run:
UPDATE users
SET role = 'admin'
WHERE email = 'admin@macrochef.local';
```

**Create Trainer User**:
```sql
-- Same process as admin, but with role = 'trainer'
UPDATE users
SET role = 'trainer'
WHERE email = 'trainer@macrochef.local';
```

**Create Regular User**:
```sql
-- Users created via signup are 'user' by default
-- No SQL update needed unless changing from another role
UPDATE users
SET role = 'user'
WHERE email = 'user@macrochef.local';
```

**Verify Roles**:
```sql
SELECT id, email, name, role, email_verified, created_at
FROM users
ORDER BY created_at DESC;
```

## Testing User Role (Regular Users)

### Access Granted

**Dashboard**:
- URL: `http://localhost:3000/`
- Features: Personal meal plans, recipes, workouts, nutrition tracking

**Meal Planning**:
- Create/edit/delete own meal plans
- Add recipes to meal plans
- Generate shopping lists from meal plans

**Recipes**:
- Create personal recipes
- Share recipes with household
- View public recipes

**Workouts**:
- Log workouts
- Track exercises
- View workout history

**Nutrition Tracking**:
- Log food diary entries
- Track macros
- View nutrition goals

**Body Measurements**:
- Log weight, body fat percentage, measurements
- View progress charts

**Household Management**:
- Create/join households
- Invite members
- Share meal plans and shopping lists

### Access Denied

**Admin Dashboard**:
- URL: `http://localhost:3000/admin`
- Expected: Redirect to `/` (home)

**Trainer Dashboard**:
- URL: `http://localhost:3000/trainer`
- Expected: Redirect to `/` (home)

**User Management**:
- URL: `http://localhost:3000/admin/users`
- Expected: Redirect to `/` (home)

### Test Scenarios

**Scenario 1: Create and Access Meal Plan**
1. Log in as user: `user@macrochef.local`
2. Navigate to Meal Plans
3. Create a new meal plan
4. Verify meal plan appears in list
5. Log in as different user
6. Attempt to access first user's meal plan by ID
7. Expected: 403 Forbidden or redirect

**Scenario 2: Household Sharing**
1. User A creates household
2. User A invites User B
3. User B accepts invitation
4. User A creates meal plan for household
5. User B views household meal plans
6. Expected: User B can see User A's household meal plan
7. User C (not in household) attempts to view
8. Expected: 403 Forbidden

**Scenario 3: Recipe Privacy**
1. User creates private recipe (isPublic = false)
2. Log in as different user
3. Attempt to view recipe
4. Expected: 403 Forbidden or not found
5. Change recipe to public (isPublic = true)
6. Log in as different user
7. Expected: Recipe is visible

## Testing Trainer Role

### Access Granted

**All User Features**: Trainers inherit all user role permissions

**Trainer Dashboard**:
- URL: `http://localhost:3000/trainer`
- Features: Client management, workout assignment, messaging

**Client Management**:
- View client list
- View client profiles (with consent)
- Access client nutrition data (if allowed)
- Access client workout logs (if allowed)
- Access client measurements (if allowed)

**Workout Assignment**:
- Create workouts for clients
- Assign exercises
- Track client progress

**Messaging**:
- Chat with clients via SignalR
- Send workout/recipe recommendations
- Real-time notifications

### Access Denied

**Admin Dashboard**:
- URL: `http://localhost:3000/admin`
- Expected: Redirect to `/` (home)

**User Management**:
- URL: `http://localhost:3000/admin/users`
- Expected: Redirect to `/` (home)

### Test Scenarios

**Scenario 1: Client Relationship**
1. Log in as trainer: `trainer@macrochef.local`
2. Navigate to Trainer Dashboard
3. Send client invitation to `user@macrochef.local`
4. Log in as user, accept invitation
5. Log back in as trainer
6. Expected: User appears in client list with "active" status

**Scenario 2: View Client Nutrition (With Permission)**
1. Trainer-client relationship exists
2. Client grants nutrition viewing permission:
   ```sql
   UPDATE trainer_client_relationships
   SET can_view_nutrition = true
   WHERE trainer_id = '<trainer-id>' AND client_id = '<client-id>';
   ```
3. Log in as trainer
4. Navigate to client's profile
5. Expected: Can view nutrition data

**Scenario 3: View Client Nutrition (Without Permission)**
1. Trainer-client relationship exists
2. Client denies nutrition viewing permission:
   ```sql
   UPDATE trainer_client_relationships
   SET can_view_nutrition = false
   WHERE trainer_id = '<trainer-id>' AND client_id = '<client-id>';
   ```
3. Log in as trainer
4. Navigate to client's profile
5. Expected: Nutrition data is hidden or 403 Forbidden

**Scenario 4: Chat/Messaging**
1. Trainer-client relationship exists with `can_message = true`
2. Log in as trainer
3. Open chat with client
4. Send message
5. Log in as client
6. Expected: Message appears in real-time via SignalR
7. Client replies
8. Expected: Trainer receives message in real-time

**Scenario 5: Assign Workout**
1. Log in as trainer
2. Navigate to client's profile
3. Create new workout program
4. Assign to client
5. Log in as client
6. Expected: Workout appears in client's workout list
7. Client logs sets/reps
8. Log in as trainer
9. Expected: Progress is visible to trainer

## Testing Admin Role

### Access Granted

**All Features**: Admins have unrestricted access to entire system

**Admin Dashboard**:
- URL: `http://localhost:3000/admin`
- Features: User management, system stats, analytics

**User Management**:
- URL: `http://localhost:3000/admin/users`
- Create users with any role
- Update user roles
- Ban/unban users
- Set user passwords
- Impersonate users
- Revoke sessions
- Delete users

**Relationship Management**:
- URL: `http://localhost:3000/admin/relationships`
- View all trainer-client relationships
- Modify permissions
- End relationships

**System Access**:
- View all users' data (meal plans, recipes, workouts)
- Access all households
- View all chat conversations

### Access Denied

None - admins have full system access.

### Test Scenarios

**Scenario 1: Create User with Role**
1. Log in as admin: `admin@macrochef.local`
2. Navigate to `http://localhost:3000/admin/users`
3. Click "Create User"
4. Fill form with role = "trainer"
5. Submit
6. Expected: User created with trainer role
7. Log out, log in as new trainer
8. Expected: Trainer dashboard is accessible

**Scenario 2: Change User Role**
1. Log in as admin
2. Navigate to User Management
3. Click on a user with role = "user"
4. Click "Change Role"
5. Select "trainer"
6. Confirm
7. Expected: User's role updated to trainer
8. Verify in database:
   ```sql
   SELECT email, role FROM users WHERE email = '<user-email>';
   ```

**Scenario 3: Ban User**
1. Log in as admin
2. Navigate to user detail page
3. Click "Ban User"
4. Enter reason: "Testing ban functionality"
5. Set expiry: 7 days
6. Confirm
7. Log out
8. Attempt to log in as banned user
9. Expected: Login fails with ban message
10. Log in as admin
11. Click "Unban User"
12. Expected: User can log in again

**Scenario 4: Impersonate User**
1. Log in as admin
2. Navigate to user detail page
3. Click "Impersonate User"
4. Expected: Redirected to home page as that user
5. Verify session shows impersonated user's data
6. Expected: Access only features allowed for impersonated user's role
7. Log out to end impersonation session

**Scenario 5: Revoke Sessions**
1. User logs in from multiple devices/browsers
2. Log in as admin
3. Navigate to user detail page
4. Click "Revoke All Sessions"
5. Expected: All user's sessions are invalidated
6. User refreshes their browser
7. Expected: Redirected to login page

**Scenario 6: Set User Password**
1. Log in as admin
2. Navigate to user detail page
3. Click "Set Password"
4. Enter new password: "NewPassword123!"
5. Confirm
6. Log out
7. Log in as that user with new password
8. Expected: Login succeeds

**Scenario 7: Delete User**
1. Log in as admin
2. Create test user or use existing
3. Navigate to user detail page
4. Click "Delete User"
5. Confirm deletion
6. Expected: User removed from system
7. Verify in database:
   ```sql
   SELECT * FROM users WHERE email = '<deleted-user-email>';
   ```
8. Expected: No results

## Session and Token Testing

### Valid Session

**Test Fresh Session**:
1. Log in with any user
2. Navigate to any protected page
3. Expected: Access granted
4. Check browser dev tools > Application > Cookies
5. Verify JWT token exists with:
   - httpOnly: true
   - sameSite: lax
   - secure: true (in production)

### Expired Session

**Test Token Expiry** (JWT expires in 15 minutes):
1. Log in
2. Wait 15 minutes (or manually modify token expiry in database)
3. Navigate to any protected page
4. Expected: Session validation fails
5. Expected: Redirect to `/login`
6. Expected: Error toast: "Session expired. Please log in again."

### Invalid Token

**Test Malformed Token**:
1. Log in
2. Open browser dev tools > Application > Cookies
3. Manually modify JWT cookie value (corrupt it)
4. Navigate to any protected page
5. Expected: Session cleared
6. Expected: Redirect to `/login`
7. Expected: Error message displayed

### Session Hijacking Prevention

**Test CSRF Protection**:
1. Log in
2. Open browser dev tools > Network
3. Observe POST requests to `/api/auth/*`
4. Verify CSRF token in headers
5. Attempt to forge request without CSRF token
6. Expected: 403 Forbidden

## Database Verification Queries

### Check User Roles
```sql
SELECT
  id,
  email,
  name,
  role,
  email_verified,
  banned,
  created_at
FROM users
ORDER BY created_at DESC;
```

### Check Active Sessions
```sql
SELECT
  s.id,
  s.user_id,
  u.email,
  u.role,
  s.expires_at,
  s.ip_address,
  s.user_agent,
  s.created_at
FROM sessions s
JOIN users u ON s.user_id = u.id
WHERE s.expires_at > NOW()
ORDER BY s.created_at DESC;
```

### Check Trainer-Client Relationships
```sql
SELECT
  tcr.id,
  t.email AS trainer_email,
  c.email AS client_email,
  tcr.status,
  tcr.can_view_nutrition,
  tcr.can_view_workouts,
  tcr.can_view_measurements,
  tcr.can_message,
  tcr.created_at
FROM trainer_client_relationships tcr
JOIN users t ON tcr.trainer_id = t.id
JOIN users c ON tcr.client_id = c.id
ORDER BY tcr.created_at DESC;
```

### Check Household Memberships
```sql
SELECT
  h.id,
  h.name AS household_name,
  u.email AS member_email,
  hm.role AS household_role,
  hm.can_edit_recipes,
  hm.can_edit_shopping_list,
  hm.can_view_nutrition,
  hm.joined_at
FROM households h
JOIN household_members hm ON h.id = hm.household_id
JOIN users u ON hm.user_id = u.id
ORDER BY h.created_at DESC, hm.joined_at;
```

### Check User Permissions
```sql
-- Get all permissions for a specific user
SELECT
  u.email,
  u.role,
  CASE
    WHEN u.role = 'admin' THEN 'Full system access'
    WHEN u.role = 'trainer' THEN 'User features + client management'
    WHEN u.role = 'user' THEN 'Personal data only'
    ELSE 'Unknown role'
  END AS permission_level,
  COUNT(DISTINCT hm.household_id) AS household_count,
  COUNT(DISTINCT tcr_trainer.id) AS clients_count,
  COUNT(DISTINCT tcr_client.id) AS trainers_count
FROM users u
LEFT JOIN household_members hm ON u.id = hm.user_id
LEFT JOIN trainer_client_relationships tcr_trainer ON u.id = tcr_trainer.trainer_id
LEFT JOIN trainer_client_relationships tcr_client ON u.id = tcr_client.client_id
WHERE u.email = '<user-email>'
GROUP BY u.id, u.email, u.role;
```

## API Testing with curl

### Test Authentication
```bash
# Sign up
curl -X POST http://localhost:3000/api/auth/sign-up \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "Test123!",
    "name": "Test User"
  }'

# Sign in
curl -X POST http://localhost:3000/api/auth/sign-in \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "Test123!"
  }' \
  -c cookies.txt

# Access protected endpoint with session
curl -X GET http://localhost:3000/api/Users/me \
  -b cookies.txt
```

### Test Authorization
```bash
# Get JWT token
TOKEN=$(curl -s -X POST http://localhost:3000/api/auth/sign-in \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@example.com","password":"Admin123!"}' \
  | jq -r '.token')

# Access admin endpoint
curl -X GET http://localhost:3000/api/Users \
  -H "Authorization: Bearer $TOKEN"

# Expected: 200 if admin, 403 if not admin
```

## Common Issues and Solutions

### Issue: "Access Denied" when accessing admin panel

**Cause**: User role is not 'admin'

**Solution**:
```sql
UPDATE users SET role = 'admin' WHERE email = '<your-email>';
```

### Issue: "Session expired" immediately after login

**Cause**: JWT expiry misconfiguration or clock skew

**Solution**:
1. Check JWT configuration in `frontend/lib/auth.ts`
2. Verify token expiry setting (default: 15 minutes)
3. Check server/client clock synchronization

### Issue: Trainer cannot view client data

**Cause**: Permissions not granted in relationship

**Solution**:
```sql
UPDATE trainer_client_relationships
SET
  can_view_nutrition = true,
  can_view_workouts = true,
  can_view_measurements = true
WHERE trainer_id = '<trainer-id>' AND client_id = '<client-id>';
```

### Issue: Role change doesn't take effect

**Cause**: Session cached with old role

**Solution**:
1. Log out completely
2. Clear browser cookies
3. Log in again
4. Or revoke all sessions as admin

### Issue: Cannot create admin user via UI

**Cause**: First admin must be created via SQL

**Solution**: Use SQL commands in "Option 2: SQL Commands" section above

## Security Best Practices

1. **Never use default passwords in production**
   - Change all test passwords before deploying
   - Enforce strong password policy

2. **Regularly rotate JWT secrets**
   - Update `BETTER_AUTH_SECRET` environment variable
   - Invalidates all existing sessions

3. **Monitor admin actions**
   - Log all admin operations (impersonation, role changes, deletions)
   - Review logs regularly for suspicious activity

4. **Limit admin accounts**
   - Only create admin accounts when absolutely necessary
   - Consider multi-factor authentication for admins

5. **Test in isolated environment**
   - Use separate database for testing
   - Never test with production data

6. **Validate authorization at every layer**
   - Frontend: UI visibility based on role
   - Backend: Enforce permissions in API handlers
   - Database: Row-level security (if using Postgres RLS)

## Automated Testing

### Jest/Vitest Tests (Frontend)
```typescript
// Example: Test role-based component rendering
describe('AdminDashboard', () => {
  it('redirects non-admin users', async () => {
    const session = { user: { role: 'user' } };
    render(<AdminDashboard session={session} />);
    expect(window.location.pathname).toBe('/');
  });

  it('renders for admin users', async () => {
    const session = { user: { role: 'admin' } };
    render(<AdminDashboard session={session} />);
    expect(screen.getByText('User Management')).toBeInTheDocument();
  });
});
```

### xUnit Tests (Backend)
```csharp
// Example: Test authorization handler
[Fact]
public async Task ShoppingListAuthorizationHandler_Owner_Succeeds()
{
    // Arrange
    var user = new ClaimsPrincipal(new ClaimsIdentity(new[] {
        new Claim(ClaimTypes.NameIdentifier, userId)
    }));
    var shoppingList = new ShoppingList { UserId = Guid.Parse(userId) };

    // Act
    var result = await _authorizationService.AuthorizeAsync(
        user, shoppingList, new OwnershipRequirement()
    );

    // Assert
    Assert.True(result.Succeeded);
}
```

## Next Steps

After completing role testing:

1. **Implement authorization fixes** from `.context/security-fixes.md`
2. **Add integration tests** for all protected endpoints
3. **Document new features** in user manual
4. **Security audit** before production deployment

## References

- BetterAuth Documentation: https://www.better-auth.com/docs
- Access Control Design: `.context/access-control-design.md`
- Security Fixes Plan: `.context/security-fixes.md`
- Admin Features: `.context/admin-features.md`
