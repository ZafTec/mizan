"use client";

import { useRouter } from "next/navigation";
import { useState, useTransition } from "react";
import {
  createHousehold,
  setActiveHousehold,
  type HouseholdSummary,
} from "@/data/household";
import { appToast } from "@/lib/toast";
import { Select } from "@/components/ui/select";

export function HouseholdSwitcherForm({
  households,
  activeId,
}: {
  households: HouseholdSummary[];
  activeId: string | null;
}) {
  const router = useRouter();
  const [pending, startTransition] = useTransition();

  if (households.length === 0) {
    return (
      <p className="text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
        Create or join a household to pick an active one.
      </p>
    );
  }

  const handleChange = (next: string) => {
    const value = next === "" ? null : next;
    startTransition(async () => {
      const result = await setActiveHousehold(value);
      if (result.success) {
        appToast.success("Active household updated.");
        router.refresh();
      } else {
        appToast.error(result.message ?? "Could not switch household.");
      }
    });
  };

  return (
    <label className="inline-flex items-center gap-3 text-sm">
      <span className="sr-only">Active household</span>
      <Select
        className="w-auto min-w-[220px]"
        value={activeId ?? ""}
        onChange={handleChange}
        disabled={pending}
        aria-label="Active household"
        options={[
          { value: "", label: "No active household" },
          ...households.map((h) => ({ value: h.id, label: h.name })),
        ]}
      />
    </label>
  );
}

export function CreateHouseholdForm() {
  const router = useRouter();
  const [name, setName] = useState("");
  const [pending, startTransition] = useTransition();

  const handleSubmit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const trimmed = name.trim();
    if (!trimmed) return;

    startTransition(async () => {
      const result = await createHousehold(trimmed);
      if (result) {
        appToast.success("Household created.");
        setName("");
        router.push(`/profile/household?household=${result.id}`);
      } else {
        appToast.error("Could not create household.");
      }
    });
  };

  return (
    <form
      onSubmit={handleSubmit}
      className="flex flex-col gap-3 sm:flex-row sm:items-center"
    >
      <input
        type="text"
        required
        maxLength={100}
        value={name}
        onChange={(e) => setName(e.target.value)}
        placeholder="e.g. The Rossi family"
        className="input h-10 flex-1"
        aria-label="Household name"
        disabled={pending}
      />
      <button
        type="submit"
        className="btn-primary h-10"
        disabled={pending || name.trim().length === 0}
      >
        {pending ? "Creating..." : "Create"}
      </button>
    </form>
  );
}
