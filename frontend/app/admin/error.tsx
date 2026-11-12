"use client";

import { useEffect } from "react";
import Link from "next/link";
import AdminPageHeader from "@/components/admin/AdminPageHeader";

export default function AdminError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    console.error("Admin panel error:", error);
  }, [error]);

  return (
    <>
      <AdminPageHeader
        title="This page did not load"
        lead="Try again; if it keeps failing, the API logs say why."
      />
      {error.message && (
        <p className="mb-6 border-y border-border py-3 font-mono text-[13px] break-all text-muted-foreground">
          {error.message}
        </p>
      )}
      <div className="flex flex-wrap gap-3">
        <button type="button" onClick={reset} className="btn-primary">
          Try again
        </button>
        <Link href="/admin" className="btn-secondary">
          Overview
        </Link>
      </div>
    </>
  );
}
