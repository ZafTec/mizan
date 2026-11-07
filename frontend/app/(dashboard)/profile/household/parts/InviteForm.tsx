"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import { inviteToHousehold } from "@/data/household";
import { appToast } from "@/lib/toast";
import { Select } from "@/components/ui/select";

export function InviteForm({ householdId }: { householdId: string }) {
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [role, setRole] = useState<"admin" | "member">("member");
  const [pending, startTransition] = useTransition();

  const handleSubmit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const trimmed = email.trim();
    if (!trimmed) return;
    startTransition(async () => {
      const result = await inviteToHousehold(householdId, trimmed, role);
      if (result.success) {
        appToast.success("Invitation sent.");
        setEmail("");
        router.refresh();
      } else {
        appToast.error(result.message ?? "Could not send invitation.");
      }
    });
  };

  return (
    <form
      onSubmit={handleSubmit}
      className="flex flex-col gap-3 sm:flex-row sm:items-center"
    >
      <input
        type="email"
        required
        maxLength={255}
        value={email}
        onChange={(e) => setEmail(e.target.value)}
        placeholder="friend@example.com"
        className="input h-10 flex-1"
        aria-label="Invitee email"
        disabled={pending}
      />
      <Select
        className="sm:w-40"
        value={role}
        onChange={(next) => setRole(next as "admin" | "member")}
        aria-label="Role"
        disabled={pending}
        options={[
          { value: "member", label: "Member" },
          { value: "admin", label: "Admin" },
        ]}
      />
      <button
        type="submit"
        className="btn-primary h-10"
        disabled={pending || email.trim().length === 0}
      >
        {pending ? "Sending..." : "Invite"}
      </button>
    </form>
  );
}
