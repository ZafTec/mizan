/**
 * A `callbackUrl` or `redirect` query parameter is attacker-controlled: anyone
 * can send a link with `?callbackUrl=https://evil.example`. Only same-origin
 * paths are honoured, and everything else falls back.
 */
const SAFE_PATH = /^\/[A-Za-z0-9\-._~!$&'()*+,;=:@/]*(\?[A-Za-z0-9\-._~!$&'()*+,;=:@/%?]*)?(#[A-Za-z0-9\-._~!$&'()*+,;=:@/%?]*)?$/;

export function safeRedirectPath(candidate: string | null | undefined, fallback = "/dashboard"): string {
	if (!candidate) return fallback;
	// "//host" and "/\host" are protocol-relative and leave the origin.
	if (candidate.startsWith("//") || candidate.startsWith("/\\")) return fallback;
	return SAFE_PATH.test(candidate) ? candidate : fallback;
}
