"use client";

import { useState } from "react";
import {
  deleteAdminUser,
  impersonateAdminUser,
  revokeAdminUserSessions,
  updateAdminUser,
} from "@/lib/api/admin-users";
import { useRouter } from "next/navigation";
import ConfirmationModal from "@/components/ConfirmationModal";
import { ModalShell } from "@/components/ModalShell";
import { appToast } from "@/lib/toast";
import { Select } from "@/components/ui/select";

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
  const [showRevokeSessionsConfirm, setShowRevokeSessionsConfirm] =
    useState(false);

  async function handleBanUser(reason: string, banExpires: string | null) {
    setIsLoading(true);
    setError(null);

    try {
      await updateAdminUser(user.id, {
        banned: true,
        banReason: reason,
        banExpires,
      });

      setShowBanDialog(false);
      appToast.success("Account suspended");
      router.refresh();
    } catch (err) {
      appToast.error(err, "Could not suspend the account");
      setError(
        err instanceof Error ? err.message : "Could not suspend the account",
      );
    } finally {
      setIsLoading(false);
    }
  }

  async function handleUnbanUser() {
    setIsLoading(true);
    setError(null);

    try {
      await updateAdminUser(user.id, { banned: false });

      appToast.success("Suspension lifted");
      router.refresh();
    } catch (err) {
      appToast.error(err, "Could not lift the suspension");
      setError(
        err instanceof Error ? err.message : "Could not lift the suspension",
      );
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
      setError(err instanceof Error ? err.message : "Failed to set password");
    } finally {
      setIsLoading(false);
    }
  }

  async function handleViewAs() {
    setIsLoading(true);
    setError(null);

    try {
      await impersonateAdminUser(user.id);
      // A full load, so every server-rendered screen is drawn as this user.
      // eslint-disable-next-line @next/next/no-location-assign-relative-destination -- a full load is the point
      window.location.assign("/today");
    } catch (err) {
      appToast.error(err, "Could not open the site as this user");
      setError(
        err instanceof Error
          ? err.message
          : "Could not open the site as this user",
      );
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
        err instanceof Error ? err.message : "Failed to revoke sessions",
      );
    } finally {
      setIsLoading(false);
    }
  }

  return (
    <div className="space-y-3">
      {error && (
        <div className="rounded-xs border border-burnt-peach-300 bg-burnt-peach-50 px-3 py-2.5 text-sm text-burnt-peach-800 dark:border-burnt-peach-500/30 dark:bg-burnt-peach-500/10 dark:text-burnt-peach-300">
          {error}
        </div>
      )}

      {!user.banned ? (
        <button
          type="button"
          onClick={() => setShowBanDialog(true)}
          disabled={isLoading}
          className="btn-danger w-full"
        >
          Suspend account
        </button>
      ) : (
        <button
          type="button"
          onClick={handleUnbanUser}
          disabled={isLoading}
          className="btn-primary w-full"
        >
          Lift suspension
        </button>
      )}

      {user.role !== "admin" && !user.banned && (
        <button
          type="button"
          onClick={handleViewAs}
          disabled={isLoading}
          className="btn-secondary w-full"
        >
          View the site as this user
        </button>
      )}

      <button
        type="button"
        onClick={() => setShowRoleDialog(true)}
        disabled={isLoading}
        className="btn-secondary w-full"
      >
        Change role
      </button>

      <button
        type="button"
        onClick={() => setShowPasswordDialog(true)}
        disabled={isLoading}
        className="btn-secondary w-full"
      >
        Set password
      </button>

      <button
        type="button"
        onClick={() => setShowRevokeSessionsConfirm(true)}
        disabled={isLoading}
        className="btn-secondary w-full"
      >
        Revoke all sessions
      </button>

      <button
        type="button"
        onClick={() => setShowDeleteConfirm(true)}
        disabled={isLoading}
        className="btn-danger w-full"
      >
        Delete user
      </button>

      <BanDialog
        open={showBanDialog}
        isLoading={isLoading}
        onConfirm={handleBanUser}
        onCancel={() => setShowBanDialog(false)}
      />

      <RoleDialog
        open={showRoleDialog}
        isLoading={isLoading}
        currentRole={user.role || "user"}
        onConfirm={handleSetRole}
        onCancel={() => setShowRoleDialog(false)}
      />

      <PasswordDialog
        open={showPasswordDialog}
        isLoading={isLoading}
        onConfirm={handleSetPassword}
        onCancel={() => setShowPasswordDialog(false)}
      />

      <DeleteConfirmDialog
        open={showDeleteConfirm}
        isLoading={isLoading}
        userName={user.name || user.email}
        onConfirm={handleDeleteUser}
        onCancel={() => setShowDeleteConfirm(false)}
      />

      <ConfirmationModal
        isOpen={showRevokeSessionsConfirm}
        onClose={() => setShowRevokeSessionsConfirm(false)}
        onConfirm={handleRevokeAllSessions}
        title="Revoke all sessions"
        message="This will sign the user out on every device."
        confirmText="Revoke sessions"
        isDanger
        isLoading={isLoading}
      />
    </div>
  );
}

function BanDialog({
  open,
  isLoading,
  onConfirm,
  onCancel,
}: {
  open: boolean;
  isLoading: boolean;
  onConfirm: (reason: string, banExpires: string | null) => void;
  onCancel: () => void;
}) {
  const [reason, setReason] = useState("");
  // A local date and time from the picker; empty means until lifted.
  const [ends, setEnds] = useState("");
  // A disabled submit button with no explanation reads as "broken" - this is
  // what made the ban dialog look like it was not dismissing, when it was
  // actually just refusing an empty reason silently.
  const [touched, setTouched] = useState(false);

  function submit() {
    const trimmed = reason.trim();
    if (!trimmed) {
      setTouched(true);
      return;
    }
    onConfirm(trimmed, ends ? new Date(ends).toISOString() : null);
  }

  function close() {
    setReason("");
    setEnds("");
    setTouched(false);
    onCancel();
  }

  return (
    <ModalShell open={open} onClose={close}>
      <div className="card w-full space-y-4 p-5">
        <h3 className="text-base font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
          Suspend account
        </h3>
        <p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
          The person cannot sign in until the suspension ends or is lifted.
        </p>
        <div className="space-y-4">
          <div>
            <label className="label" htmlFor="suspend-reason">
              Reason
            </label>
            <textarea
              id="suspend-reason"
              value={reason}
              onChange={(e) => {
                setReason(e.target.value);
                if (touched) setTouched(false);
              }}
              className="input"
              rows={3}
              placeholder="Why is this account being suspended?"
            />
            {touched && (
              <p className="mt-1.5 text-xs text-burnt-peach-700 dark:text-burnt-peach-400">
                A reason is required.
              </p>
            )}
          </div>
          <div>
            <label className="label" htmlFor="suspend-ends">
              Ends (optional)
            </label>
            <input
              id="suspend-ends"
              type="datetime-local"
              value={ends}
              onChange={(e) => setEnds(e.target.value)}
              className="input"
              aria-describedby="suspend-ends-hint"
            />
            <p
              id="suspend-ends-hint"
              className="mt-1 text-xs text-charcoal-blue-500 dark:text-charcoal-blue-400"
            >
              Leave empty to suspend until someone lifts it.
            </p>
          </div>
          <div className="flex gap-2">
            <button
              type="button"
              onClick={close}
              disabled={isLoading}
              className="btn-ghost flex-1"
            >
              Cancel
            </button>
            <button
              type="button"
              onClick={submit}
              disabled={isLoading}
              className="btn-danger flex-1"
            >
              {isLoading ? "Suspending…" : "Suspend"}
            </button>
          </div>
        </div>
      </div>
    </ModalShell>
  );
}

function RoleDialog({
  open,
  isLoading,
  currentRole,
  onConfirm,
  onCancel,
}: {
  open: boolean;
  isLoading: boolean;
  currentRole: string;
  onConfirm: (role: Role) => void;
  onCancel: () => void;
}) {
  const [role, setRole] = useState<Role>(
    currentRole === "admin" || currentRole === "trainer" ? currentRole : "user",
  );

  return (
    <ModalShell open={open} onClose={onCancel}>
      <div className="card w-full space-y-4 p-5">
        <h3 className="text-base font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
          Change role
        </h3>
        <div className="space-y-4">
          <div>
            <label htmlFor="new-role" className="label">
              New role
            </label>
            <Select
              id="new-role"
              value={role}
              onChange={(next) => setRole(next as Role)}
              options={[
                { value: "user", label: "User" },
                { value: "trainer", label: "Trainer" },
                { value: "admin", label: "Admin" },
              ]}
            />
          </div>
          <div className="flex gap-2">
            <button
              type="button"
              onClick={onCancel}
              disabled={isLoading}
              className="btn-ghost flex-1"
            >
              Cancel
            </button>
            <button
              type="button"
              onClick={() => onConfirm(role)}
              disabled={isLoading}
              className="btn-primary flex-1"
            >
              {isLoading ? "Updating…" : "Update role"}
            </button>
          </div>
        </div>
      </div>
    </ModalShell>
  );
}

function PasswordDialog({
  open,
  isLoading,
  onConfirm,
  onCancel,
}: {
  open: boolean;
  isLoading: boolean;
  onConfirm: (password: string) => void;
  onCancel: () => void;
}) {
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const mismatch =
    password !== "" && confirmPassword !== "" && password !== confirmPassword;

  function close() {
    setPassword("");
    setConfirmPassword("");
    onCancel();
  }

  return (
    <ModalShell open={open} onClose={close}>
      <div className="card w-full space-y-4 p-5">
        <h3 className="text-base font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
          Set password
        </h3>
        <div className="space-y-4">
          <div>
            <label className="label">New password</label>
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className="input"
              placeholder="Enter new password…"
            />
          </div>
          <div>
            <label className="label">Confirm password</label>
            <input
              type="password"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              className="input"
              placeholder="Confirm password…"
            />
          </div>
          {mismatch && (
            <p className="text-xs text-burnt-peach-700 dark:text-burnt-peach-400">
              Passwords do not match.
            </p>
          )}
          <div className="flex gap-2">
            <button
              type="button"
              onClick={close}
              disabled={isLoading}
              className="btn-ghost flex-1"
            >
              Cancel
            </button>
            <button
              type="button"
              onClick={() => onConfirm(password)}
              disabled={isLoading || !password || mismatch}
              className="btn-primary flex-1"
            >
              {isLoading ? "Saving…" : "Set password"}
            </button>
          </div>
        </div>
      </div>
    </ModalShell>
  );
}

function DeleteConfirmDialog({
  open,
  isLoading,
  userName,
  onConfirm,
  onCancel,
}: {
  open: boolean;
  isLoading: boolean;
  userName: string;
  onConfirm: () => void;
  onCancel: () => void;
}) {
  return (
    <ModalShell open={open} onClose={onCancel}>
      <div className="card w-full space-y-4 p-5">
        <h3 className="text-base font-semibold text-burnt-peach-700 dark:text-burnt-peach-400">
          Delete user
        </h3>
        <p className="text-sm text-charcoal-blue-600 dark:text-charcoal-blue-400">
          Are you sure you want to delete{" "}
          <strong className="text-charcoal-blue-900 dark:text-charcoal-blue-50">
            {userName}
          </strong>
          ? This cannot be undone.
        </p>
        <div className="flex gap-2">
          <button
            type="button"
            onClick={onCancel}
            disabled={isLoading}
            className="btn-ghost flex-1"
          >
            Cancel
          </button>
          <button
            type="button"
            onClick={onConfirm}
            disabled={isLoading}
            className="btn-danger flex-1"
          >
            {isLoading ? "Deleting…" : "Delete permanently"}
          </button>
        </div>
      </div>
    </ModalShell>
  );
}
