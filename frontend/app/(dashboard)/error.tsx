"use client";

import { useEffect } from "react";
import Link from "next/link";
import { CircleAlert } from "lucide-react";

export default function DashboardError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    console.error("Dashboard error:", error);
  }, [error]);
  return (
    <div className="log-page py-12">
      <div className="max-w-md">
        <CircleAlert
          size={28}
          className="text-destructive mb-5"
          aria-hidden="true"
        />
        <h1 className="text-2xl font-semibold">Something went wrong</h1>
        <p className="log-muted mt-3 leading-relaxed">
          This page couldn't load. Your saved entries are still there. Try again
          to reconnect.
        </p>
        <div className="flex flex-wrap gap-3 mt-6">
          <button onClick={reset} className="btn-primary">
            Try again
          </button>
          <Link href="/today" className="btn-secondary">
            Back to Today
          </Link>
        </div>
      </div>
    </div>
  );
}
