"use client";

import { useState } from "react";
import {
  deleteAdminUser,
  revokeAdminUserSessions,
  updateAdminUser,
} from "@/lib/api/admin-users";
import { useRouter } from "next/navigation";
import ConfirmationModal from "@/components/ConfirmationModal";
import { ModalShell } from "@/components/ModalShell";
import { appToast } from "@/lib/toast";

type Role = "user" | "trainer" | "admin";

interface User {
  id: string;
  name?: string | null;
  email: string;
  role: string | null;
  banned: boolean | null;
  banReason?: string | null;
  banExpires?: string | Date | null;
}

export function UserActions({ user }: { user: User }) {
  const router = useRouter();
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [showBanDialog, setShowBanDialog] = useState(false);
  const [showRoleDialog, setShowRoleDialog] = useState(false);
  const [showPasswordDialog, setShowPasswordDialog] = useState(false);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [showRevokeSessionsConfirm, setShowRevokeSessionsConfirm] = useState(false);

  async function handleBanUser(reason: string, expiresInDays?: number) {
    setIsLoading(true);
    setError(null);

    try {
      const banExpires = expiresInDays
        ? new Date(Date.now() + expiresInDays * 24 * 60 * 60 * 1000).toISOString()
        : null;

      await updateAdminUser(user.id, { banned: true, banReason: reason, banExpires });

      setShowBanDialog(false);
      appToast.success("User banned");
      router.refresh();
    } catch (err) {
      appToast.error(err, "Failed to ban user");
      setError(err instanceof Error ? err.message : "Failed to ban user");
    } finally {
      setIsLoading(false);
    }
  }

  async function handleUnbanUser() {
    setIsLoading(true);
    setError(null);

    try {
      await updateAdminUser(user.id, { banned: false });

      appToast.success("User unbanned");
      router.refresh();
    } catch (err) {
      appToast.error(err, "Failed to unban user");
      setError(err instanceof Error ? err.message : "Failed to unban user");
    } finally {
      setIsLoading(false);
    }
  }

  async function handleSetRole(role: Role) {
    setIsLoading(true);
    setError(null);

    try {
      // Trainer is grantable again: the backend owns the users table since v2,
      // so all three roles go through one endpoint.
      await updateAdminUser(user.id, { role });

      setShowRoleDialog(false);
      appToast.success("User role updated");
      router.refresh();
    } catch (err) {
      appToast.error(err, "Failed to set role");
      setError(err instanceof Error ? err.message : "Failed to set role");
    } finally {
      setIsLoading(false);
    }
  }

  async function handleSetPassword(newPassword: string) {
    setIsLoading(true);
    setError(null);

    try {
      await updateAdminUser(user.id, { newPassword });

      setShowPasswordDialog(false);
      appToast.success("Password updated");
      router.refresh();
    } catch (err) {
      appToast.error(err, "Failed to set password");
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
      await deleteAdminUser(user.id);

      appToast.success("User deleted");
      router.push("/admin/users");
    } catch (err) {
      appToast.error(err, "Failed to delete user");
      setError(err instanceof Error ? err.message : "Failed to delete user");
    } finally {
      setIsLoading(false);
    }
  }

  async function handleRevokeAllSessions() {
    setIsLoading(true);
    setError(null);

    try {
      await revokeAdminUserSessions(user.id);

      appToast.success("All sessions revoked");
      setShowRevokeSessionsConfirm(false);
      router.refresh();
    } catch (err) {
      appToast.error(err, "Failed to revoke sessions");
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
        onClick={() => setShowRevokeSessionsConfirm(true)}
        disabled={isLoading}
        className="w-full px-4 py-2 border rounded-lg hover:bg-accent disabled:opacity-50"
      >
        Revoke All Sessions
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

      <ConfirmationModal
        isOpen={showRevokeSessionsConfirm}
        onClose={() => setShowRevokeSessionsConfirm(false)}
        onConfirm={handleRevokeAllSessions}
        title="Revoke All Sessions"
        message="This will sign the user out on every device."
        confirmText="Revoke Sessions"
        isDanger
        isLoading={isLoading}
      />
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
    <ModalShell open onClose={onCancel}>
      <div className="bg-card rounded-lg p-6 w-full">
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
    </ModalShell>
  );
}

function RoleDialog({
  currentRole,
  onConfirm,
  onCancel,
}: {
  currentRole: string;
  onConfirm: (role: Role) => void;
  onCancel: () => void;
}) {
  const [role, setRole] = useState<Role>(
    currentRole === "admin" || currentRole === "trainer" ? currentRole : "user"
  );

  return (
    <ModalShell open onClose={onCancel}>
      <div className="bg-card rounded-lg p-6 w-full">
        <h3 className="text-lg font-semibold mb-4">Change Role</h3>
        <div className="space-y-4">
          <div>
            <label className="block text-sm font-medium mb-2">New Role</label>
            <select
              value={role}
              onChange={(e) => setRole(e.target.value as Role)}
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
    </ModalShell>
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
    <ModalShell open onClose={onCancel}>
      <div className="bg-card rounded-lg p-6 w-full">
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
    </ModalShell>
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
    <ModalShell open onClose={onCancel}>
      <div className="bg-card rounded-lg p-6 w-full">
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
    </ModalShell>
  );
}
