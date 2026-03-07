const LOCAL_API_ORIGIN = "http://localhost:5000";

function normalizeApiOrigin(rawValue?: string | null): string | null {
  if (!rawValue) return null;

  const trimmedValue = rawValue.trim();
  if (!trimmedValue) return null;

  try {
    const url = new URL(trimmedValue);
    url.pathname = url.pathname.replace(/\/api\/?$/, "");
    url.search = "";
    url.hash = "";
    return url.toString().replace(/\/$/, "");
  } catch {
    return trimmedValue.replace(/\/api\/?$/, "").replace(/\/$/, "") || null;
  }
}

function isLocalHostname(hostname: string): boolean {
  return hostname === "localhost" || hostname === "127.0.0.1";
}

export function resolvePublicApiOrigin(): string {
  const configuredClientOrigin = normalizeApiOrigin(process.env.NEXT_PUBLIC_API_URL);

  if (typeof window === "undefined") {
    return configuredClientOrigin ?? normalizeApiOrigin(process.env.API_URL) ?? LOCAL_API_ORIGIN;
  }

  if (configuredClientOrigin) {
    try {
      if (new URL(configuredClientOrigin).origin !== window.location.origin) {
        return configuredClientOrigin;
      }
    } catch {
      return configuredClientOrigin;
    }
  }

  if (isLocalHostname(window.location.hostname)) {
    return LOCAL_API_ORIGIN;
  }

  return `${window.location.protocol}//api.${window.location.hostname}`;
}
