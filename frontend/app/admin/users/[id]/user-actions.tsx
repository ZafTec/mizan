"use client";

import { useState } from "react";
import {
  deleteAdminUser,
  impersonateAdminUser,
  revokeAdminUserSessions,
  updateAdminUser,
} from "@/lib/api/admin-users";
import { useRouter } from "next/navigation";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import { Modal } from "@/components/ui/dialog";
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

  // The confirmation dialogs show a failure themselves and stay open.
  async function handleDeleteUser() {
    await deleteAdminUser(user.id);
    appToast.success("User deleted");
    router.push("/admin/users");
  }

  async function handleRevokeAllSessions() {
    await revokeAdminUserSessions(user.id);
    appToast.success("All sessions revoked");
    router.refresh();
  }

  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-wrap gap-2">
        {user.role !== "admin" && !user.banned && (
          <button
            type="button"
            onClick={handleViewAs}
            disabled={isLoading}
            className="btn-primary"
          >
            View as this person
          </button>
        )}

        <button
          type="button"
          onClick={() => setShowRoleDialog(true)}
          disabled={isLoading}
          className="btn-secondary"
        >
          Change role
        </button>

        <button
          type="button"
          onClick={() => setShowPasswordDialog(true)}
          disabled={isLoading}
          className="btn-secondary"
        >
          Set password
        </button>

        <button
          type="button"
          onClick={() => setShowRevokeSessionsConfirm(true)}
          disabled={isLoading}
          className="btn-secondary"
        >
          Revoke all sessions
        </button>

        {!user.banned ? (
          <button
            type="button"
            onClick={() => setShowBanDialog(true)}
            disabled={isLoading}
            className="btn-secondary-destructive"
          >
            Suspend account
          </button>
        ) : (
          <button
            type="button"
            onClick={handleUnbanUser}
            disabled={isLoading}
            className="btn-secondary"
          >
            Lift suspension
          </button>
        )}

        <button
          type="button"
          onClick={() => setShowDeleteConfirm(true)}
          disabled={isLoading}
          className="btn-ghost-destructive"
        >
          Delete user
        </button>
      </div>

      {error && (
        <p role="alert" className="text-[14px] text-destructive">
          {error}
        </p>
      )}

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

      <ConfirmDialog
        open={showDeleteConfirm}
        onClose={() => setShowDeleteConfirm(false)}
        onConfirm={handleDeleteUser}
        title={`Delete ${user.name || user.email}?`}
        description="The account and everything it logged are deleted. This cannot be undone."
        confirmLabel="Delete permanently"
        pendingLabel="Deleting…"
        errorFallback="Failed to delete user"
      />

      <ConfirmDialog
        open={showRevokeSessionsConfirm}
        onClose={() => setShowRevokeSessionsConfirm(false)}
        onConfirm={handleRevokeAllSessions}
        title="Revoke all of this user's sessions?"
        description="They are signed out on every device and have to sign in again."
        confirmLabel="Revoke sessions"
        pendingLabel="Revoking…"
        errorFallback="Failed to revoke sessions"
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
    <Modal
      open={open}
      onClose={close}
      title="Suspend account"
      description="The person cannot sign in until the suspension ends or is lifted."
      footer={
        <>
          <button
            type="button"
            onClick={close}
            disabled={isLoading}
            className="btn-ghost"
          >
            Cancel
          </button>
          <button
            type="button"
            onClick={submit}
            disabled={isLoading}
            className="btn-danger"
          >
            {isLoading ? "Suspending…" : "Suspend"}
          </button>
        </>
      }
    >
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
            <p className="mt-1.5 text-xs text-destructive">
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
      </div>
    </Modal>
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
    <Modal
      open={open}
      onClose={onCancel}
      title="Change role"
      footer={
        <>
          <button
            type="button"
            onClick={onCancel}
            disabled={isLoading}
            className="btn-ghost"
          >
            Cancel
          </button>
          <button
            type="button"
            onClick={() => onConfirm(role)}
            disabled={isLoading}
            className="btn-primary"
          >
            {isLoading ? "Updating…" : "Update role"}
          </button>
        </>
      }
    >
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
      </div>
    </Modal>
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
    <Modal
      open={open}
      onClose={close}
      title="Set password"
      footer={
        <>
          <button
            type="button"
            onClick={close}
            disabled={isLoading}
            className="btn-ghost"
          >
            Cancel
          </button>
          <button
            type="button"
            onClick={() => onConfirm(password)}
            disabled={isLoading || !password || mismatch}
            className="btn-primary"
          >
            {isLoading ? "Saving…" : "Set password"}
          </button>
        </>
      }
    >
      <div className="space-y-4">
        <div>
          <label className="label" htmlFor="new-password">
            New password
          </label>
          <input
            id="new-password"
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            className="input"
            placeholder="Enter new password…"
          />
        </div>
        <div>
          <label className="label" htmlFor="confirm-password">
            Confirm password
          </label>
          <input
            id="confirm-password"
            type="password"
            value={confirmPassword}
            onChange={(e) => setConfirmPassword(e.target.value)}
            className="input"
            placeholder="Confirm password…"
          />
        </div>
        {mismatch && (
          <p className="text-xs text-destructive">Passwords do not match.</p>
        )}
      </div>
    </Modal>
  );
}
