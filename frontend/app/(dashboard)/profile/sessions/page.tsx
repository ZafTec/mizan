"use client";

import { useSession, authClient } from "@/lib/auth-client";
import { useState, useEffect } from "react";
import Loading from "@/components/Loading";
import { useRouter } from "next/navigation";
import Link from "next/link";
import ConfirmationModal from "@/components/ConfirmationModal";
import { appToast } from "@/lib/toast";

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
  const [sessionToRevoke, setSessionToRevoke] = useState<string | null>(null);
  const [confirmRevokeAll, setConfirmRevokeAll] = useState(false);

  useEffect(() => {
    if (currentSession?.user) {
      fetchSessions();
    }
  }, [currentSession]);

  async function fetchSessions() {
    try {
      const result = await authClient.listSessions();
      if (result.data) {
        setSessions(
          result.data.map((s: any) => ({
            id: s.id,
            token: s.token,
            ipAddress: s.ipAddress || undefined,
            userAgent: s.userAgent || undefined,
            createdAt: typeof s.createdAt === "string" ? s.createdAt : new Date(s.createdAt).toISOString(),
            expiresAt: typeof s.expiresAt === "string" ? s.expiresAt : new Date(s.expiresAt).toISOString(),
          }))
        );
      }
    } catch (error) {
      console.error("Failed to fetch sessions:", error);
    } finally {
      setIsLoading(false);
    }
  }

  async function handleRevokeSession(sessionToken: string) {
    setRevoking(sessionToken);
    try {
      await authClient.revokeSession({ token: sessionToken });
	      appToast.success("Session revoked");
      setSessions((prev) => prev.filter((s) => s.token !== sessionToken));
    } catch (error) {
      console.error("Failed to revoke session:", error);
	      appToast.error(error, "Failed to revoke session");
    } finally {
      setRevoking(null);
	      setSessionToRevoke(null);
    }
  }

  async function handleRevokeAllOther() {
    setRevoking("all");
    try {
      await authClient.revokeSessions();
	      appToast.success("All other sessions revoked");
      await fetchSessions();
    } catch (error) {
      console.error("Failed to revoke other sessions:", error);
	      appToast.error(error, "Failed to revoke other sessions");
    } finally {
      setRevoking(null);
	      setConfirmRevokeAll(false);
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
    const ua = userAgent.toLowerCase();
    let browser = "Unknown browser";
    let os = "Unknown OS";

    if (ua.includes("chrome") && !ua.includes("edge")) browser = "Chrome";
    else if (ua.includes("firefox")) browser = "Firefox";
    else if (ua.includes("safari") && !ua.includes("chrome")) browser = "Safari";
    else if (ua.includes("edge")) browser = "Edge";

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
      <div className="flex items-center justify-center min-h-[50vh]">
        <Loading />
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
	      <div className="flex items-center justify-between">
	        <div>
	          <h1 className="text-2xl font-bold text-slate-900 dark:text-slate-100">Active Sessions</h1>
	          <p className="mt-1 text-slate-500 dark:text-slate-400">
	            Manage devices where you're currently logged in
	          </p>
        </div>
        <Link href="/profile/settings" className="btn-secondary">
          <i className="ri-arrow-left-line" />
          Back to Settings
        </Link>
      </div>

	      <div className="card p-6">
	        <div className="flex items-center justify-between">
	          <div>
	            <p className="text-sm text-slate-500 dark:text-slate-400">Active Sessions</p>
	            <p className="text-3xl font-bold text-slate-900 dark:text-slate-100">{activeSessions.length}</p>
	          </div>
	          {activeSessions.length > 1 && (
	            <button
	              onClick={() => setConfirmRevokeAll(true)}
	              disabled={revoking === "all"}
	              className="rounded-xl border border-red-200 px-4 py-2 text-red-600 transition-colors hover:bg-red-50 disabled:opacity-50 dark:border-red-500/30 dark:text-red-300 dark:hover:bg-red-500/10"
	            >
              {revoking === "all" ? "Revoking..." : "Revoke All Other Sessions"}
            </button>
          )}
        </div>
      </div>

      <div className="space-y-4">
	        {activeSessions.length === 0 ? (
	          <div className="card p-12 text-center">
	            <i className="ri-lock-line mb-4 text-6xl text-slate-300 dark:text-slate-600" />
	            <h2 className="mb-2 text-xl font-semibold text-slate-900 dark:text-slate-100">
	              No Active Sessions
	            </h2>
	            <p className="text-slate-500 dark:text-slate-400">
	              You don't have any active sessions
	            </p>
          </div>
        ) : (
          activeSessions.map((session) => {
            const isCurrent = isCurrentSession(session);
            return (
              <div
                key={session.id}
                className={`card p-6 ${isCurrent ? "border-2 border-brand-500" : ""}`}
              >
                <div className="flex items-start gap-4">
					<div className="w-12 h-12 rounded-2xl bg-brand-600 flex items-center justify-center shrink-0 dark:bg-brand-500">
                    <i className={`${getDeviceIcon(session.userAgent)} text-xl text-white`} />
                  </div>

                  <div className="flex-1 min-w-0">
	                  <div className="flex items-center gap-2 mb-1">
	                      <h3 className="font-semibold text-slate-900 dark:text-slate-100">
	                        {getDeviceInfo(session.userAgent)}
	                      </h3>
	                      {isCurrent && (
	                        <span className="rounded-full bg-brand-100 px-2 py-0.5 text-xs font-medium text-brand-800 dark:bg-brand-950/60 dark:text-brand-200">
	                          Current
	                        </span>
	                      )}
	                    </div>

	                    <div className="space-y-1 text-sm text-slate-500 dark:text-slate-400">
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
	                      onClick={() => setSessionToRevoke(session.token)}
	                      disabled={revoking === session.token}
	                      className="shrink-0 rounded-xl border border-red-200 px-4 py-2 text-red-600 transition-colors hover:bg-red-50 disabled:opacity-50 dark:border-red-500/30 dark:text-red-300 dark:hover:bg-red-500/10"
	                    >
                      {revoking === session.token ? "Revoking..." : "Revoke"}
                    </button>
                  )}
                </div>
              </div>
            );
          })
        )}
      </div>

	      <div className="card border-blue-200 bg-blue-50 p-6 dark:border-blue-500/30 dark:bg-blue-500/10">
	        <div className="flex gap-3">
	          <i className="ri-information-line shrink-0 text-xl text-blue-600 dark:text-blue-300" />
	          <div>
	            <h3 className="mb-1 font-semibold text-blue-900 dark:text-blue-100">Security Tip</h3>
	            <p className="text-sm text-blue-800 dark:text-blue-200">
	              If you see a session you don't recognize, revoke it immediately and change your password.
              This helps protect your account from unauthorized access.
            </p>
          </div>
        </div>
	      </div>

	      <ConfirmationModal
	        isOpen={!!sessionToRevoke}
	        onClose={() => setSessionToRevoke(null)}
	        onConfirm={() => sessionToRevoke && handleRevokeSession(sessionToRevoke)}
	        title="Revoke Session"
	        message="Are you sure you want to revoke this session?"
	        confirmText="Revoke Session"
	        isDanger
	        isLoading={!!sessionToRevoke && revoking === sessionToRevoke}
	      />

	      <ConfirmationModal
	        isOpen={confirmRevokeAll}
	        onClose={() => setConfirmRevokeAll(false)}
	        onConfirm={handleRevokeAllOther}
	        title="Revoke All Other Sessions"
	        message="This will sign you out everywhere except this device."
	        confirmText="Revoke Sessions"
	        isDanger
	        isLoading={revoking === "all"}
	      />
    </div>
  );
}
