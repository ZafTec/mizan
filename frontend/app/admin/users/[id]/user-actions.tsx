"use client";

import { useState } from "react";
import { authClient } from "@/lib/auth-client";
import { useRouter } from "next/navigation";

interface User {
  id: string;
  name: string | null;
  email: string;
  role: string | null;
  banned: boolean | null;
  banReason: string | null;
  banExpires: Date | null;
}

export function UserActions({ user }: { user: User }) {
  const router = useRouter();
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [showBanDialog, setShowBanDialog] = useState(false);
  const [showRoleDialog, setShowRoleDialog] = useState(false);
  const [showPasswordDialog, setShowPasswordDialog] = useState(false);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);

  async function handleBanUser(reason: string, expiresInDays?: number) {
    setIsLoading(true);
    setError(null);

    try {
      const expiresAt = expiresInDays
        ? new Date(Date.now() + expiresInDays * 24 * 60 * 60 * 1000)
        : undefined;

      await authClient.admin.banUser({
        userId: user.id,
        banReason: reason,
        banExpiresIn: expiresInDays ? expiresInDays * 24 * 60 * 60 : undefined,
      });

      setShowBanDialog(false);
      router.refresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to ban user");
    } finally {
      setIsLoading(false);
    }
  }

  async function handleUnbanUser() {
    setIsLoading(true);
    setError(null);

    try {
      await authClient.admin.unbanUser({
        userId: user.id,
      });

      router.refresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to unban user");
    } finally {
      setIsLoading(false);
    }
  }

  async function handleSetRole(role: string) {
    setIsLoading(true);
    setError(null);

    try {
      // BetterAuth only supports "user" and "admin" roles
      // "trainer" is a separate business concept managed by the backend
      if (role === "user" || role === "admin") {
        await authClient.admin.setRole({
          userId: user.id,
          role: role as "user" | "admin",
        });
      }
      // TODO: If role is "trainer", call backend API to set trainer status

      setShowRoleDialog(false);
      router.refresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to set role");
    } finally {
      setIsLoading(false);
    }
  }

  async function handleSetPassword(newPassword: string) {
    setIsLoading(true);
    setError(null);

    try {
      await authClient.admin.setUserPassword({
        userId: user.id,
        newPassword: newPassword,
      });

      setShowPasswordDialog(false);
      alert("Password updated successfully");
      router.refresh();
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Failed to set password"
      );
    } finally {
      setIsLoading(false);
    }
  }

  async function handleDeleteUser() {
    setIsLoading(true);
    setError(null);

    try {
      await authClient.admin.removeUser({
        userId: user.id,
      });

      router.push("/admin/users");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to delete user");
    } finally {
      setIsLoading(false);
    }
  }

  async function handleImpersonateUser() {
    setIsLoading(true);
    setError(null);

    try {
      await authClient.admin.impersonateUser({
        userId: user.id,
      });

      router.push("/");
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Failed to impersonate user"
      );
    } finally {
      setIsLoading(false);
    }
  }

  async function handleRevokeAllSessions() {
    if (!confirm("Revoke all sessions for this user?")) return;

    setIsLoading(true);
    setError(null);

    try {
      await authClient.admin.revokeUserSessions({
        userId: user.id,
      });

      alert("All sessions revoked");
      router.refresh();
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Failed to revoke sessions"
      );
    } finally {
      setIsLoading(false);
    }
  }

  return (
    <div className="space-y-3">
      {error && (
        <div className="p-3 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg">
          <p className="text-sm text-red-800 dark:text-red-200">{error}</p>
        </div>
      )}

      {!user.banned ? (
        <button
          onClick={() => setShowBanDialog(true)}
          disabled={isLoading}
          className="w-full px-4 py-2 bg-red-600 text-white rounded-lg hover:bg-red-700 disabled:opacity-50"
        >
          Ban User
        </button>
      ) : (
        <button
          onClick={handleUnbanUser}
          disabled={isLoading}
          className="w-full px-4 py-2 bg-green-600 text-white rounded-lg hover:bg-green-700 disabled:opacity-50"
        >
          Unban User
        </button>
      )}

      <button
        onClick={() => setShowRoleDialog(true)}
        disabled={isLoading}
        className="w-full px-4 py-2 border rounded-lg hover:bg-accent disabled:opacity-50"
      >
        Change Role
      </button>

      <button
        onClick={() => setShowPasswordDialog(true)}
        disabled={isLoading}
        className="w-full px-4 py-2 border rounded-lg hover:bg-accent disabled:opacity-50"
      >
        Set Password
      </button>

      <button
        onClick={handleRevokeAllSessions}
        disabled={isLoading}
        className="w-full px-4 py-2 border rounded-lg hover:bg-accent disabled:opacity-50"
      >
        Revoke All Sessions
      </button>

      <button
        onClick={handleImpersonateUser}
        disabled={isLoading}
        className="w-full px-4 py-2 border rounded-lg hover:bg-accent disabled:opacity-50"
      >
        Impersonate User
      </button>

      <button
        onClick={() => setShowDeleteConfirm(true)}
        disabled={isLoading}
        className="w-full px-4 py-2 bg-red-600 text-white rounded-lg hover:bg-red-700 disabled:opacity-50"
      >
        Delete User
      </button>

      {showBanDialog && (
        <BanDialog
          onConfirm={handleBanUser}
          onCancel={() => setShowBanDialog(false)}
        />
      )}

      {showRoleDialog && (
        <RoleDialog
          currentRole={user.role || "user"}
          onConfirm={handleSetRole}
          onCancel={() => setShowRoleDialog(false)}
        />
      )}

      {showPasswordDialog && (
        <PasswordDialog
          onConfirm={handleSetPassword}
          onCancel={() => setShowPasswordDialog(false)}
        />
      )}

      {showDeleteConfirm && (
        <DeleteConfirmDialog
          userName={user.name || user.email}
          onConfirm={handleDeleteUser}
          onCancel={() => setShowDeleteConfirm(false)}
        />
      )}
    </div>
  );
}

function BanDialog({
  onConfirm,
  onCancel,
}: {
  onConfirm: (reason: string, expiresInDays?: number) => void;
  onCancel: () => void;
}) {
  const [reason, setReason] = useState("");
  const [expiresInDays, setExpiresInDays] = useState<number | undefined>();

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
      <div className="bg-card rounded-lg p-6 max-w-md w-full mx-4">
        <h3 className="text-lg font-semibold mb-4">Ban User</h3>
        <div className="space-y-4">
          <div>
            <label className="block text-sm font-medium mb-2">Reason</label>
            <textarea
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              className="w-full px-3 py-2 border rounded-lg focus:outline-none focus:ring-2 focus:ring-primary"
              rows={3}
              placeholder="Enter ban reason..."
            />
          </div>
          <div>
            <label className="block text-sm font-medium mb-2">
              Expires In (days, leave empty for permanent)
            </label>
            <input
              type="number"
              value={expiresInDays || ""}
              onChange={(e) =>
                setExpiresInDays(
                  e.target.value ? parseInt(e.target.value) : undefined
                )
              }
              className="w-full px-3 py-2 border rounded-lg focus:outline-none focus:ring-2 focus:ring-primary"
              placeholder="Leave empty for permanent ban"
            />
          </div>
          <div className="flex gap-2">
            <button
              onClick={() => onConfirm(reason, expiresInDays)}
              disabled={!reason}
              className="flex-1 px-4 py-2 bg-red-600 text-white rounded-lg hover:bg-red-700 disabled:opacity-50"
            >
              Ban User
            </button>
            <button
              onClick={onCancel}
              className="flex-1 px-4 py-2 border rounded-lg hover:bg-accent"
            >
              Cancel
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

function RoleDialog({
  currentRole,
  onConfirm,
  onCancel,
}: {
  currentRole: string;
  onConfirm: (role: string) => void;
  onCancel: () => void;
}) {
  const [role, setRole] = useState(currentRole);

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
      <div className="bg-card rounded-lg p-6 max-w-md w-full mx-4">
        <h3 className="text-lg font-semibold mb-4">Change Role</h3>
        <div className="space-y-4">
          <div>
            <label className="block text-sm font-medium mb-2">New Role</label>
            <select
              value={role}
              onChange={(e) => setRole(e.target.value)}
              className="w-full px-3 py-2 border rounded-lg focus:outline-none focus:ring-2 focus:ring-primary"
            >
              <option value="user">User</option>
              <option value="trainer">Trainer</option>
              <option value="admin">Admin</option>
            </select>
          </div>
          <div className="flex gap-2">
            <button
              onClick={() => onConfirm(role)}
              className="flex-1 px-4 py-2 bg-primary text-primary-foreground rounded-lg hover:bg-primary/90"
            >
              Update Role
            </button>
            <button
              onClick={onCancel}
              className="flex-1 px-4 py-2 border rounded-lg hover:bg-accent"
            >
              Cancel
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

function PasswordDialog({
  onConfirm,
  onCancel,
}: {
  onConfirm: (password: string) => void;
  onCancel: () => void;
}) {
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
      <div className="bg-card rounded-lg p-6 max-w-md w-full mx-4">
        <h3 className="text-lg font-semibold mb-4">Set Password</h3>
        <div className="space-y-4">
          <div>
            <label className="block text-sm font-medium mb-2">
              New Password
            </label>
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className="w-full px-3 py-2 border rounded-lg focus:outline-none focus:ring-2 focus:ring-primary"
              placeholder="Enter new password..."
            />
          </div>
          <div>
            <label className="block text-sm font-medium mb-2">
              Confirm Password
            </label>
            <input
              type="password"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              className="w-full px-3 py-2 border rounded-lg focus:outline-none focus:ring-2 focus:ring-primary"
              placeholder="Confirm password..."
            />
          </div>
          {password && confirmPassword && password !== confirmPassword && (
            <p className="text-sm text-red-600">Passwords do not match</p>
          )}
          <div className="flex gap-2">
            <button
              onClick={() => onConfirm(password)}
              disabled={!password || password !== confirmPassword}
              className="flex-1 px-4 py-2 bg-primary text-primary-foreground rounded-lg hover:bg-primary/90 disabled:opacity-50"
            >
              Set Password
            </button>
            <button
              onClick={onCancel}
              className="flex-1 px-4 py-2 border rounded-lg hover:bg-accent"
            >
              Cancel
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

function DeleteConfirmDialog({
  userName,
  onConfirm,
  onCancel,
}: {
  userName: string;
  onConfirm: () => void;
  onCancel: () => void;
}) {
  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50">
      <div className="bg-card rounded-lg p-6 max-w-md w-full mx-4">
        <h3 className="text-lg font-semibold mb-4 text-red-600">
          Delete User
        </h3>
        <p className="mb-4">
          Are you sure you want to delete <strong>{userName}</strong>? This
          action cannot be undone.
        </p>
        <div className="flex gap-2">
          <button
            onClick={onConfirm}
            className="flex-1 px-4 py-2 bg-red-600 text-white rounded-lg hover:bg-red-700"
          >
            Delete Permanently
          </button>
          <button
            onClick={onCancel}
            className="flex-1 px-4 py-2 border rounded-lg hover:bg-accent"
          >
            Cancel
          </button>
        </div>
      </div>
    </div>
  );
}
