# Security policy

We take the security of Mizan seriously. Thank you for helping keep it, and
the people who use it, safe.

## Supported versions

Mizan is delivered as a rolling release. Only the current `main` and
`subscriptions` branches, and the latest Docker images tagged `latest`, are
supported and will receive security fixes. Self-hosted deployments running
older images should update before reporting issues.

| Version | Supported |
| --- | --- |
| `main` / `subscriptions` (HEAD) | Yes |
| `euaell/mizan-*:latest` images | Yes |
| Older tags / forks | No |

## Reporting a vulnerability

**Please do not open a public GitHub issue for security problems.**

Use one of these private channels instead:

1. **Preferred, GitHub private advisory:**
   [https://github.com/ZafTec/mizan/security/advisories/new](https://github.com/ZafTec/mizan/security/advisories/new).
   This is end-to-end private, lets us coordinate a fix with you, and
   auto-assigns a CVE if we publish.
2. **Email:** [contact@zaftech.co](mailto:contact@zaftech.co). If you want
   encrypted mail, ask us for a PGP key in your first message.

Include:

- A description of the issue and its impact.
- Reproduction steps, commands, requests, payloads, or a proof of concept.
- Affected versions, endpoints, or files.
- Your name or handle if you'd like credit in the advisory.

## Our response

We aim for:

- **Acknowledgement within 72 hours.**
- **Triage and severity decision within 7 days.**
- **Fix or mitigation for critical / high issues within 30 days**; lower
  severity may take longer but we'll keep you updated.

Once a fix is ready we'll coordinate a disclosure timeline with you and, if
the issue affects production data, notify affected users.

## Scope

**In scope:**

- This repository (backend `Mizan.Api`, `Mizan.Application`,
  `Mizan.Infrastructure`, `Mizan.Mcp.Server`; frontend under `frontend/`).
- The hosted product at `https://mizan.zaftech.co` and its API at
  `https://api.mizan.zaftech.co`.
- Published Docker images under `euaell/mizan-*`.
- The MCP server surface and its tool catalog.
- Authentication and authorization (BetterAuth JWT issuance, backend JWT
  validation, household access control, trainer/client isolation).
- Multi-tenant data isolation (households, recipes marked private vs.
  shared, trainer-client relationships).
- Billing / subscription entitlement checks (once wired).

**Out of scope:**

- Findings that require physical access to a user's device.
- Social engineering against Zaftech staff or users.
- Denial of service via resource exhaustion on a single account (we rate
  limit, but soak-testing the platform is not considered a vulnerability).
- Missing SPF/DKIM/DMARC on non-production subdomains.
- Version disclosure in server headers.
- Self-XSS or issues that need an already-compromised browser.
- Issues in third-party dependencies with no exploitable path in Mizan -
  report those upstream (Dependabot covers these).
- Vulnerabilities in self-hosted forks running old code.

## Safe harbor

We will not pursue legal action against researchers who:

- Act in good faith to avoid privacy violations, service disruption, or
  data destruction.
- Give us a reasonable time to respond before public disclosure
  (we default to **90 days**; sooner if we ship a fix and confirm).
- Only interact with accounts they own or have explicit permission to
  test.
- Do not attempt to pivot beyond the minimum access needed to demonstrate
  the issue.

If you follow this policy, we will treat your report as authorized
testing under the Computer Fraud and Abuse Act (US) and the Computer
Misuse Act (UK) and equivalents, and we will not file complaints with
your host, ISP, or employer.

## Rewards

We don't currently run a paid bug bounty. We do credit researchers in the
published advisory, and for significant findings we're happy to provide a
Lifetime subscription to Mizan as a thank-you.

## Hardening and supply chain

- All production containers run as non-root users.
- Secrets are injected via environment variables sourced from a locked-down
  `.env` on the VPS; none are checked into the repo.
- Browser sessions are opaque 256-bit tokens in an httpOnly cookie. Only their
  SHA-256 is stored, in `user_sessions`, cached in Redis via `HybridCache`.
  Revoking a session takes effect on the next request.
- Passwords are hashed with ASP.NET Core Identity's `PasswordHasher<T>`
  (PBKDF2-HMAC-SHA512). Five failed sign-ins lock an account for 15 minutes.
- Cross-site request forgery is blocked by three things together: the session
  cookie is `SameSite=Lax`, so it is not attached to a cross-site write; the
  API accepts JSON bodies only, so a cross-site HTML form cannot reach an
  endpoint; and CORS names the allowed origins explicitly, so the preflight
  those requests need is refused.
- OAuth return targets are validated against an allowlist of same-origin paths
  on both sides - `AppUrls.SafeReturnUrl` and `safeRedirectPath` - so a crafted
  `?returnUrl=` cannot turn sign-in into an open redirect.
- Verification and reset links are never written anywhere durable. With no SMTP
  host configured a development run prints the message to stdout; any other
  environment logs an error naming only the subject.
- Transactional mail is logged against the user id, never the address.
- Dependabot and GitHub's security advisories are enabled on this repo.
- The Paddle checkout handles all payment data; we never store card
  details.

Thank you for helping us keep Mizan safe.
