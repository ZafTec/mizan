"use client";

import { useSession, authClient } from "@/lib/auth-client";
import { useState, useEffect } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";

interface Session {
  id: string;
  token: string;
  ipAddress?: string;
  userAgent?: string;
  createdAt: string;
  expiresAt: string;
}

export default function ProfileSessionsPage() {
  const { data: currentSession, isPending } = useSession();
  const router = useRouter();
  const [sessions, setSessions] = useState<Session[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [revoking, setRevoking] = useState<string | null>(null);

  useEffect(() => {
    if (currentSession?.user) {
      fetchSessions();
    }
  }, [currentSession]);

  async function fetchSessions() {
    try {
      // TODO: Implement proper session listing API
      // For now, just show current session
      if (currentSession?.session) {
        setSessions([{
          id: currentSession.session.id,
          token: currentSession.session.token,
          ipAddress: currentSession.session.ipAddress || undefined,
          userAgent: currentSession.session.userAgent || undefined,
          createdAt: currentSession.session.createdAt.toISOString(),
          expiresAt: currentSession.session.expiresAt.toISOString(),
        }]);
      }
    } catch (error) {
      console.error("Failed to fetch sessions:", error);
    } finally {
      setIsLoading(false);
    }
  }

  async function handleRevokeSession(sessionId: string) {
    if (!confirm("Revoke this session?")) return;

    setRevoking(sessionId);
    try {
      // TODO: Implement proper session revoke API
      alert("Session revocation not yet implemented");
    } catch (error) {
      console.error("Failed to revoke session:", error);
      alert("Failed to revoke session");
    } finally {
      setRevoking(null);
    }
  }

  async function handleRevokeAllOther() {
    if (!confirm("Revoke all other sessions? You'll stay logged in on this device.")) return;

    setRevoking("all");
    try {
      // TODO: Implement proper session revoke API
      alert("Session revocation not yet implemented");
    } catch (error) {
      console.error("Failed to revoke other sessions:", error);
      alert("Failed to revoke other sessions");
    } finally {
      setRevoking(null);
    }
  }

  function getDeviceIcon(userAgent?: string) {
    if (!userAgent) return "ri-computer-line";
    const ua = userAgent.toLowerCase();
    if (ua.includes("mobile") || ua.includes("android") || ua.includes("iphone")) {
      return "ri-smartphone-line";
    }
    if (ua.includes("tablet") || ua.includes("ipad")) {
      return "ri-tablet-line";
    }
    return "ri-computer-line";
  }

  function getDeviceInfo(userAgent?: string) {
    if (!userAgent) return "Unknown device";

    // Simple user agent parsing
    const ua = userAgent.toLowerCase();
    let browser = "Unknown browser";
    let os = "Unknown OS";

    // Browser detection
    if (ua.includes("chrome")) browser = "Chrome";
    else if (ua.includes("firefox")) browser = "Firefox";
    else if (ua.includes("safari")) browser = "Safari";
    else if (ua.includes("edge")) browser = "Edge";

    // OS detection
    if (ua.includes("windows")) os = "Windows";
    else if (ua.includes("mac")) os = "macOS";
    else if (ua.includes("linux")) os = "Linux";
    else if (ua.includes("android")) os = "Android";
    else if (ua.includes("ios") || ua.includes("iphone") || ua.includes("ipad")) os = "iOS";

    return `${browser} on ${os}`;
  }

  function isCurrentSession(session: Session) {
    return session.token === currentSession?.session?.token;
  }

  if (isPending || isLoading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-brand-500"></div>
      </div>
    );
  }

  if (!currentSession?.user) {
    router.push("/login");
    return null;
  }

  const activeSessions = sessions.filter((s) => new Date(s.expiresAt) > new Date());

  return (
    <div className="max-w-3xl mx-auto space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-slate-900">Active Sessions</h1>
          <p className="text-slate-500 mt-1">
            Manage devices where you're currently logged in
          </p>
        </div>
        <Link href="/profile/settings" className="btn-secondary">
          ← Back to Settings
        </Link>
      </div>

      {/* Session Count & Actions */}
      <div className="card p-6">
        <div className="flex items-center justify-between">
          <div>
            <p className="text-sm text-slate-500">Active Sessions</p>
            <p className="text-3xl font-bold text-slate-900">{activeSessions.length}</p>
          </div>
          {activeSessions.length > 1 && (
            <button
              onClick={handleRevokeAllOther}
              disabled={revoking === "all"}
              className="px-4 py-2 rounded-xl border border-red-200 text-red-600 hover:bg-red-50 transition-colors disabled:opacity-50"
            >
              {revoking === "all" ? "Revoking..." : "Revoke All Other Sessions"}
            </button>
          )}
        </div>
      </div>

      {/* Sessions List */}
      <div className="space-y-4">
        {activeSessions.length === 0 ? (
          <div className="card p-12 text-center">
            <div className="text-6xl mb-4">🔒</div>
            <h2 className="text-xl font-semibold text-slate-900 mb-2">
              No Active Sessions
            </h2>
            <p className="text-slate-500">
              You don't have any active sessions
            </p>
          </div>
        ) : (
          activeSessions.map((session) => {
            const isCurrent = isCurrentSession(session);
            return (
              <div
                key={session.id}
                className={`card p-6 ${
                  isCurrent ? "border-2 border-brand-500" : ""
                }`}
              >
                <div className="flex items-start gap-4">
                  <div className="w-12 h-12 rounded-2xl bg-gradient-to-br from-brand-400 to-brand-600 flex items-center justify-center flex-shrink-0">
                    <i className={`${getDeviceIcon(session.userAgent)} text-xl text-white`} />
                  </div>

                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2 mb-1">
                      <h3 className="font-semibold text-slate-900">
                        {getDeviceInfo(session.userAgent)}
                      </h3>
                      {isCurrent && (
                        <span className="px-2 py-0.5 rounded-full text-xs font-medium bg-brand-100 text-brand-800">
                          Current
                        </span>
                      )}
                    </div>

                    <div className="space-y-1 text-sm text-slate-500">
                      {session.ipAddress && (
                        <p className="flex items-center gap-2">
                          <i className="ri-map-pin-line" />
                          {session.ipAddress}
                        </p>
                      )}
                      <p className="flex items-center gap-2">
                        <i className="ri-time-line" />
                        Started {new Date(session.createdAt).toLocaleString()}
                      </p>
                      <p className="flex items-center gap-2">
                        <i className="ri-calendar-line" />
                        Expires {new Date(session.expiresAt).toLocaleString()}
                      </p>
                    </div>
                  </div>

                  {!isCurrent && (
                    <button
                      onClick={() => handleRevokeSession(session.id)}
                      disabled={revoking === session.id}
                      className="px-4 py-2 rounded-xl border border-red-200 text-red-600 hover:bg-red-50 transition-colors disabled:opacity-50 flex-shrink-0"
                    >
                      {revoking === session.id ? "Revoking..." : "Revoke"}
                    </button>
                  )}
                </div>
              </div>
            );
          })
        )}
      </div>

      {/* Security Tip */}
      <div className="card p-6 bg-blue-50 border-blue-200">
        <div className="flex gap-3">
          <i className="ri-information-line text-xl text-blue-600 flex-shrink-0" />
          <div>
            <h3 className="font-semibold text-blue-900 mb-1">Security Tip</h3>
            <p className="text-sm text-blue-800">
              If you see a session you don't recognize, revoke it immediately and change your password.
              This helps protect your account from unauthorized access.
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}
