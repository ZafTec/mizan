"use client";

import { useSession, authClient } from "@/lib/auth-client";
import { useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";

export default function ProfileSettingsPage() {
  const { data: session, isPending } = useSession();
  const router = useRouter();
  const [isUpdating, setIsUpdating] = useState(false);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
  const [showPasswordDialog, setShowPasswordDialog] = useState(false);

  // Form states
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");

  if (isPending) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-brand-500"></div>
      </div>
    );
  }

  if (!session?.user) {
    router.push("/login");
    return null;
  }

  const user = session.user;

  async function handleUpdateProfile() {
    setIsUpdating(true);
    try {
      await authClient.updateUser({
        name: name || user.name || undefined,
      });
      alert("Profile updated successfully");
      window.location.reload();
    } catch (error) {
      console.error("Failed to update profile:", error);
      alert("Failed to update profile");
    } finally {
      setIsUpdating(false);
    }
  }

  async function handleChangePassword() {
    if (newPassword !== confirmPassword) {
      alert("Passwords do not match");
      return;
    }

    if (newPassword.length < 8) {
      alert("Password must be at least 8 characters");
      return;
    }

    setIsUpdating(true);
    try {
      await authClient.changePassword({
        currentPassword,
        newPassword,
        revokeOtherSessions: true,
      });
      alert("Password changed successfully");
      setShowPasswordDialog(false);
      setCurrentPassword("");
      setNewPassword("");
      setConfirmPassword("");
    } catch (error) {
      console.error("Failed to change password:", error);
      alert("Failed to change password. Check your current password.");
    } finally {
      setIsUpdating(false);
    }
  }

  async function handleDeleteAccount() {
    setIsUpdating(true);
    try {
      await authClient.deleteUser();
      router.push("/");
    } catch (error) {
      console.error("Failed to delete account:", error);
      alert("Failed to delete account");
    } finally {
      setIsUpdating(false);
    }
  }

  return (
    <div className="max-w-3xl mx-auto space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-slate-900">Account Settings</h1>
          <p className="text-slate-500 mt-1">Manage your account information and security</p>
        </div>
        <Link href="/profile" className="btn-secondary">
          ← Back to Profile
        </Link>
      </div>

      {/* Profile Information */}
      <div className="card p-6 space-y-4">
        <h2 className="font-semibold text-slate-900 flex items-center gap-2">
          <i className="ri-user-line text-brand-500" />
          Profile Information
        </h2>

        <div className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-slate-700 mb-1">
              Name
            </label>
            <input
              type="text"
              value={name || user.name || ""}
              onChange={(e) => setName(e.target.value)}
              className="input"
              placeholder="Enter your name"
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-slate-700 mb-1">
              Email
            </label>
            <input
              type="email"
              value={user.email}
              disabled
              className="input bg-slate-50 cursor-not-allowed"
            />
            <p className="text-xs text-slate-500 mt-1">
              Email changes are not supported yet
            </p>
          </div>

          <div className="flex items-center gap-2">
            <button
              onClick={handleUpdateProfile}
              disabled={isUpdating}
              className="btn-primary"
            >
              {isUpdating ? "Saving..." : "Save Changes"}
            </button>
          </div>
        </div>
      </div>

      {/* Security */}
      <div className="card p-6 space-y-4">
        <h2 className="font-semibold text-slate-900 flex items-center gap-2">
          <i className="ri-lock-line text-brand-500" />
          Security
        </h2>

        <div className="divide-y divide-slate-100">
          <div className="flex items-center justify-between py-4">
            <div>
              <p className="font-medium text-slate-900">Password</p>
              <p className="text-sm text-slate-500">
                Change your password regularly to keep your account secure
              </p>
            </div>
            <button
              onClick={() => setShowPasswordDialog(true)}
              className="btn-secondary"
            >
              Change Password
            </button>
          </div>

          <div className="flex items-center justify-between py-4">
            <div>
              <p className="font-medium text-slate-900">Active Sessions</p>
              <p className="text-sm text-slate-500">
                Manage devices and sessions where you're logged in
              </p>
            </div>
            <Link href="/profile/sessions" className="btn-secondary">
              Manage Sessions
            </Link>
          </div>
        </div>
      </div>

      {/* Danger Zone */}
      <div className="card p-6 border-red-200 space-y-4">
        <h2 className="font-semibold text-red-600 flex items-center gap-2">
          <i className="ri-error-warning-line" />
          Danger Zone
        </h2>

        <div className="flex items-center justify-between">
          <div>
            <p className="font-medium text-slate-900">Delete Account</p>
            <p className="text-sm text-slate-500">
              Permanently delete your account and all associated data
            </p>
          </div>
          <button
            onClick={() => setShowDeleteConfirm(true)}
            className="px-4 py-2 rounded-xl border border-red-200 text-red-600 hover:bg-red-50 transition-colors"
          >
            Delete Account
          </button>
        </div>
      </div>

      {/* Change Password Dialog */}
      {showPasswordDialog && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
          <div className="card max-w-md w-full p-6 space-y-4">
            <div className="flex items-center justify-between">
              <h3 className="text-lg font-semibold text-slate-900">
                Change Password
              </h3>
              <button
                onClick={() => setShowPasswordDialog(false)}
                className="text-slate-400 hover:text-slate-600"
              >
                <i className="ri-close-line text-xl" />
              </button>
            </div>

            <div className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1">
                  Current Password
                </label>
                <input
                  type="password"
                  value={currentPassword}
                  onChange={(e) => setCurrentPassword(e.target.value)}
                  className="input"
                  placeholder="Enter current password"
                />
              </div>

              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1">
                  New Password
                </label>
                <input
                  type="password"
                  value={newPassword}
                  onChange={(e) => setNewPassword(e.target.value)}
                  className="input"
                  placeholder="Enter new password"
                />
                <p className="text-xs text-slate-500 mt-1">
                  At least 8 characters
                </p>
              </div>

              <div>
                <label className="block text-sm font-medium text-slate-700 mb-1">
                  Confirm New Password
                </label>
                <input
                  type="password"
                  value={confirmPassword}
                  onChange={(e) => setConfirmPassword(e.target.value)}
                  className="input"
                  placeholder="Confirm new password"
                />
              </div>

              {newPassword && confirmPassword && newPassword !== confirmPassword && (
                <p className="text-sm text-red-600">Passwords do not match</p>
              )}

              <div className="bg-blue-50 dark:bg-blue-900/20 border border-blue-200 dark:border-blue-800 rounded-lg p-3">
                <p className="text-sm text-blue-800 dark:text-blue-200">
                  This will sign you out on all other devices
                </p>
              </div>
            </div>

            <div className="flex gap-3">
              <button
                onClick={() => setShowPasswordDialog(false)}
                className="btn-secondary flex-1"
                disabled={isUpdating}
              >
                Cancel
              </button>
              <button
                onClick={handleChangePassword}
                className="btn-primary flex-1"
                disabled={isUpdating || !currentPassword || !newPassword || !confirmPassword}
              >
                {isUpdating ? "Updating..." : "Change Password"}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Delete Account Confirmation */}
      {showDeleteConfirm && (
        <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
          <div className="card max-w-md w-full p-6 space-y-4">
            <div className="flex items-center justify-between">
              <h3 className="text-lg font-semibold text-red-600">
                Delete Account
              </h3>
              <button
                onClick={() => setShowDeleteConfirm(false)}
                className="text-slate-400 hover:text-slate-600"
              >
                <i className="ri-close-line text-xl" />
              </button>
            </div>

            <div>
              <p className="text-slate-700 mb-2">
                Are you sure you want to delete your account? This action cannot be undone.
              </p>
              <p className="text-sm text-red-600">
                All your data including meals, recipes, and goals will be permanently deleted.
              </p>
            </div>

            <div className="flex gap-3">
              <button
                onClick={() => setShowDeleteConfirm(false)}
                className="btn-secondary flex-1"
                disabled={isUpdating}
              >
                Cancel
              </button>
              <button
                onClick={handleDeleteAccount}
                className="flex-1 px-4 py-2 bg-red-600 text-white rounded-xl hover:bg-red-700 disabled:opacity-50"
                disabled={isUpdating}
              >
                {isUpdating ? "Deleting..." : "Delete Permanently"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
