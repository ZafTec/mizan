"use client";

import { useRouter } from "next/navigation";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import { clearAppearanceCookie } from "@/lib/appearance-cookie";
import { signOut } from "@/lib/auth-client";

/** Every sign-out asks first: the web shell, the navbar and the profile page. */
export function SignOutDialog({
  open,
  onClose,
}: {
  open: boolean;
  onClose: () => void;
}) {
  const router = useRouter();
  return (
    <ConfirmDialog
      open={open}
      onClose={onClose}
      title="Sign out?"
      description="Your session on this device ends now. Sessions on your other devices stay signed in."
      confirmLabel="Sign out"
      pendingLabel="Signing out…"
      errorFallback="Could not sign out"
      onConfirm={async () => {
        await signOut();
        clearAppearanceCookie();
        router.push("/");
        router.refresh();
      }}
    />
  );
}
