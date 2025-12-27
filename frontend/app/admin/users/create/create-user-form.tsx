"use client";

import { useState } from "react";
import { authClient } from "@/lib/auth-client";
import { useRouter } from "next/navigation";

export function CreateUserForm() {
  const router = useRouter();
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [formData, setFormData] = useState({
    email: "",
    password: "",
    confirmPassword: "",
    name: "",
    role: "user" as "user" | "admin" | "trainer",
  });

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setIsLoading(true);
    setError(null);

    if (formData.password !== formData.confirmPassword) {
      setError("Passwords do not match");
      setIsLoading(false);
      return;
    }

    if (formData.password.length < 8) {
      setError("Password must be at least 8 characters long");
      setIsLoading(false);
      return;
    }

    try {
      const result = await authClient.admin.createUser({
        email: formData.email,
        password: formData.password,
        name: formData.name,
        role: formData.role === "trainer" ? "user" : formData.role,
      });

      if (result.error) {
        throw new Error(result.error.message || "Failed to create user");
      }

      if (formData.role === "trainer" && result.data?.user) {
        const roleResult = await authClient.admin.setRole({
          userId: result.data.user.id,
          role: "trainer" as any,
        });

        if (roleResult.error) {
          throw new Error(roleResult.error.message || "Failed to set trainer role");
        }
      }

      router.push("/admin/users");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create user");
    } finally {
      setIsLoading(false);
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-6">
      {error && (
        <div className="p-4 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg">
          <p className="text-sm text-red-800 dark:text-red-200">{error}</p>
        </div>
      )}

      <div>
        <label
          htmlFor="email"
          className="block text-sm font-medium mb-2"
        >
          Email <span className="text-red-500">*</span>
        </label>
        <input
          type="email"
          id="email"
          required
          value={formData.email}
          onChange={(e) =>
            setFormData({ ...formData, email: e.target.value })
          }
          className="w-full px-4 py-2 border rounded-lg focus:outline-none focus:ring-2 focus:ring-primary"
          placeholder="user@example.com"
          disabled={isLoading}
        />
      </div>

      <div>
        <label
          htmlFor="name"
          className="block text-sm font-medium mb-2"
        >
          Full Name <span className="text-red-500">*</span>
        </label>
        <input
          type="text"
          id="name"
          required
          value={formData.name}
          onChange={(e) =>
            setFormData({ ...formData, name: e.target.value })
          }
          className="w-full px-4 py-2 border rounded-lg focus:outline-none focus:ring-2 focus:ring-primary"
          placeholder="John Doe"
          disabled={isLoading}
        />
      </div>

      <div>
        <label
          htmlFor="role"
          className="block text-sm font-medium mb-2"
        >
          Role <span className="text-red-500">*</span>
        </label>
        <select
          id="role"
          required
          value={formData.role}
          onChange={(e) =>
            setFormData({
              ...formData,
              role: e.target.value as "user" | "admin" | "trainer",
            })
          }
          className="w-full px-4 py-2 border rounded-lg focus:outline-none focus:ring-2 focus:ring-primary"
          disabled={isLoading}
        >
          <option value="user">User</option>
          <option value="trainer">Trainer</option>
          <option value="admin">Admin</option>
        </select>
        <p className="mt-2 text-sm text-muted-foreground">
          Select the role for this user. Admins have full system access.
        </p>
      </div>

      <div>
        <label
          htmlFor="password"
          className="block text-sm font-medium mb-2"
        >
          Password <span className="text-red-500">*</span>
        </label>
        <input
          type="password"
          id="password"
          required
          minLength={8}
          value={formData.password}
          onChange={(e) =>
            setFormData({ ...formData, password: e.target.value })
          }
          className="w-full px-4 py-2 border rounded-lg focus:outline-none focus:ring-2 focus:ring-primary"
          placeholder="Minimum 8 characters"
          disabled={isLoading}
        />
      </div>

      <div>
        <label
          htmlFor="confirmPassword"
          className="block text-sm font-medium mb-2"
        >
          Confirm Password <span className="text-red-500">*</span>
        </label>
        <input
          type="password"
          id="confirmPassword"
          required
          minLength={8}
          value={formData.confirmPassword}
          onChange={(e) =>
            setFormData({ ...formData, confirmPassword: e.target.value })
          }
          className="w-full px-4 py-2 border rounded-lg focus:outline-none focus:ring-2 focus:ring-primary"
          placeholder="Re-enter password"
          disabled={isLoading}
        />
        {formData.password &&
          formData.confirmPassword &&
          formData.password !== formData.confirmPassword && (
            <p className="mt-2 text-sm text-red-600">
              Passwords do not match
            </p>
          )}
      </div>

      <div className="pt-4 flex gap-4">
        <button
          type="submit"
          disabled={isLoading || formData.password !== formData.confirmPassword}
          className="flex-1 px-6 py-3 bg-primary text-primary-foreground rounded-lg hover:bg-primary/90 disabled:opacity-50 font-medium"
        >
          {isLoading ? "Creating..." : "Create User"}
        </button>
        <button
          type="button"
          onClick={() => router.back()}
          disabled={isLoading}
          className="px-6 py-3 border rounded-lg hover:bg-accent disabled:opacity-50"
        >
          Cancel
        </button>
      </div>

      <div className="p-4 bg-blue-50 dark:bg-blue-900/20 border border-blue-200 dark:border-blue-800 rounded-lg">
        <p className="text-sm text-blue-800 dark:text-blue-200">
          <strong>Note:</strong> The user will not receive an email verification
          link. They can log in immediately with the credentials provided.
        </p>
      </div>
    </form>
  );
}
