# MacroChef Security Documentation

**Version:** 1.0
**Last Updated:** 2025-12-27

---

## Security Overview

MacroChef implements defense-in-depth security with multiple layers:

1. **Network Layer:** HTTPS/TLS, CORS, CSP headers
2. **Authentication Layer:** JWT with ES256, httpOnly cookies
3. **Authorization Layer:** Role-based + permission-based access control
4. **Application Layer:** Input validation, SQL injection prevention
5. **Data Layer:** Encrypted connections, parameterized queries

---

## Authentication

### JWT Token Security

**Algorithm:** ES256 (ECDSA with P-256 curve)
- **Key Size:** 256-bit
- **Token Expiry:** 15 minutes (JWT), 7 days (session)
- **Storage:** httpOnly cookie (prevents XSS)
- **Transmission:** Authorization header

**Cookie Configuration:**
- `httpOnly: true` - Prevent JavaScript access
- `sameSite: "lax"` - CSRF protection
- `secure: true` - HTTPS only (production)
- `maxAge: 7 days`

### JWT + JWKS Validation

**Authorization Header:**
- `Authorization: Bearer <jwt>` - BetterAuth-issued JWT

**Backend Validation:**
- Signature verification via JWKS (`/api/auth/jwks`)
- Issuer + audience enforcement
- User status check (exists, verified, not banned)

---

## Authorization

### Role-Based Access Control

| Role | Access Level | Protected Routes |
|------|-------------|------------------|
| `user` | Personal data only | `/dashboard`, `/recipes`, `/meals` |
| `trainer` | User + client management | `/trainer`, `/clients` |
| `admin` | Full system access | `/admin` |

### Permission-Based Access Control

Trainer-client relationships include granular permissions:
- `canViewNutrition` - Food diary access
- `canViewWorkouts` - Workout log access
- `canViewMeasurements` - Body stats access
- `canMessage` - Chat access

---

## Known Vulnerabilities (Fixed)

### December 2025 Security Audit - All Critical Issues Fixed

| ID | Component | Severity | Status | Fix Commit |
|----|-----------|----------|--------|------------|
| VULN-01 | ShoppingLists | CRITICAL | ✅ Fixed | b6acf69 |
| VULN-02 | MealPlans | CRITICAL | ✅ Fixed | b6acf69 |
| VULN-03 | Households | CRITICAL | ✅ Fixed | b6acf69 |
| VULN-04 | ChatConversations | CRITICAL | ✅ Fixed | b6acf69 |
| VULN-05 | SignalR ChatHub | CRITICAL | ✅ Fixed | b6acf69 |

**All vulnerabilities involved horizontal privilege escalation - users accessing resources they don't own. All fixed by adding ownership validation to queries.**

See SECURITY_ANALYSIS.md for detailed vulnerability descriptions and fixes.

---

## Security Best Practices

### Input Validation

**Always validate on both frontend and backend:**
- Backend: FluentValidation
- Frontend: Zod schemas (auto-generated from OpenAPI)

### SQL Injection Prevention

**Always use parameterized queries via EF Core:**
```csharp
// ✅ CORRECT
var recipe = await _context.Recipes.FirstOrDefaultAsync(r => r.Id == recipeId);

// ❌ NEVER concatenate SQL strings
```

### XSS Prevention

React automatically escapes output. Never render untrusted HTML without proper sanitization.

### CSRF Protection

- Package: `csrf-csrf`
- Pattern: Double-submit cookie
- Token endpoint: `/api/csrf`

### Password Security

**Requirements:**
- Minimum 8 characters
- Mixed case, numbers required
- Hashing: bcrypt (BetterAuth)
- Never store plaintext

---

## Session Management

**Session Storage:** PostgreSQL + Redis cache
**Invalidation:** 7-day timeout, manual logout, admin revocation
**Security:** Track IP addresses and user agents for suspicious activity detection

---

## Data Protection

### Data at Rest
- Database: SSL/TLS connections
- Passwords: bcrypt hashed
- Secrets: Environment variables only

### Data in Transit
- External: HTTPS/TLS 1.2+
- Internal (Docker): HTTP (trusted network)

---

## Secrets Management

### Environment Variables

**Generate strong secrets:**
```bash
openssl rand -base64 32
```

**Rotate every 90 days:**
- BETTER_AUTH_SECRET
- Database passwords
- API keys

---

## Security Monitoring

### Logging

**What to Log:**
- ✅ Authentication attempts
- ✅ Authorization failures
- ✅ Admin actions
- ✅ Suspicious activity

**What NOT to Log:**
- ❌ Passwords
- ❌ JWT tokens
- ❌ Sensitive user data

---

## Production Security Checklist

- [ ] All secrets rotated, strong (32+ chars)
- [ ] HTTPS enforced with valid SSL certificate
- [ ] JWT expiry set to 15 minutes
- [ ] All endpoints validate ownership
- [ ] CSRF protection enabled
- [ ] CSP headers configured
- [ ] Input validation on all endpoints
- [ ] Security events logged
- [ ] Automated backups configured
- [ ] Dependencies up to date
- [ ] Only ports 80/443 exposed

---

## Reporting Security Issues

**DO NOT** open public GitHub issues for security vulnerabilities.

**Email:** security@yourdomain.com

**Include:**
- Vulnerability description
- Steps to reproduce
- Potential impact
- Suggested fix (optional)

**Response Time:**
- Acknowledge: 48 hours
- Fix: Critical within 2 weeks

---

## Additional Resources

- SECURITY_ANALYSIS.md - Detailed vulnerability analysis
- ARCHITECTURE.md - Security architecture details
- TESTING_GUIDE.md - Security testing procedures
- DEPLOYMENT_GUIDE.md - Production security configuration
