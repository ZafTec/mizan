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
	        <div className="rounded-2xl border border-red-200 bg-red-50 p-4 dark:border-red-500/30 dark:bg-red-500/10">
	          <p className="text-sm text-red-800 dark:text-red-200">{error}</p>
	        </div>
	      )}

      <div>
	        <label htmlFor="email" className="label mb-2">
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
	          className="input"
          placeholder="user@example.com"
          disabled={isLoading}
        />
      </div>

      <div>
	        <label htmlFor="name" className="label mb-2">
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
	          className="input"
          placeholder="John Doe"
          disabled={isLoading}
        />
      </div>

      <div>
	        <label htmlFor="role" className="label mb-2">
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
	          className="input"
	          disabled={isLoading}
	        >
          <option value="user">User</option>
          <option value="trainer">Trainer</option>
          <option value="admin">Admin</option>
        </select>
	        <p className="mt-2 text-sm text-slate-500 dark:text-slate-400">
	          Select the role for this user. Admins have full system access.
	        </p>
	      </div>

	      <div>
	        <label htmlFor="password" className="label mb-2">
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
	          className="input"
          placeholder="Minimum 8 characters"
          disabled={isLoading}
        />
      </div>

	      <div>
	        <label htmlFor="confirmPassword" className="label mb-2">
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
	          className="input"
          placeholder="Re-enter password"
          disabled={isLoading}
        />
        {formData.password &&
          formData.confirmPassword &&
          formData.password !== formData.confirmPassword && (
	            <p className="mt-2 text-sm text-red-600 dark:text-red-300">
              Passwords do not match
            </p>
          )}
      </div>

	      <div className="flex gap-4 pt-4">
        <button
          type="submit"
          disabled={isLoading || formData.password !== formData.confirmPassword}
	          className="btn-primary flex-1 py-3"
        >
          {isLoading ? "Creating..." : "Create User"}
        </button>
        <button
          type="button"
          onClick={() => router.back()}
          disabled={isLoading}
	          className="btn-secondary"
        >
          Cancel
        </button>
      </div>

	      <div className="rounded-2xl border border-blue-200 bg-blue-50 p-4 dark:border-blue-500/30 dark:bg-blue-500/10">
	        <p className="text-sm text-blue-800 dark:text-blue-200">
          <strong>Note:</strong> The user will not receive an email verification
          link. They can log in immediately with the credentials provided.
        </p>
      </div>
    </form>
  );
}
