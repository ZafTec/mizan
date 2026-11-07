"use client";

import * as DialogPrimitive from "@radix-ui/react-dialog";
import { X } from "lucide-react";
import { useRouter } from "next/navigation";

/**
 * A side sheet for one row of an admin list, opened by `?open={id}` so the
 * row is a link and the URL can be shared. Closing goes back to the list
 * without the parameter. Full width on a phone, a column on a wide screen;
 * focus stays inside while it is open and Escape closes it.
 */
export default function AdminDrawer({
  title,
  description,
  closeHref,
  children,
}: {
  title: string;
  description?: string;
  closeHref: string;
  children: React.ReactNode;
}) {
  const router = useRouter();

  return (
    <DialogPrimitive.Root
      open
      onOpenChange={(open) => {
        if (!open) router.replace(closeHref, { scroll: false });
      }}
    >
      <DialogPrimitive.Portal>
        <DialogPrimitive.Overlay className="fixed inset-0 z-50 bg-black/40" />
        <DialogPrimitive.Content className="fixed inset-y-0 right-0 z-50 flex w-full max-w-lg flex-col overflow-y-auto border-l border-charcoal-blue-200 bg-background p-5 shadow-xl focus:outline-none sm:p-6 dark:border-charcoal-blue-700">
          <div className="mb-4 flex items-start justify-between gap-4">
            <div className="min-w-0">
              <DialogPrimitive.Title className="break-words text-lg font-semibold text-charcoal-blue-900 dark:text-charcoal-blue-50">
                {title}
              </DialogPrimitive.Title>
              {description ? (
                <DialogPrimitive.Description className="mt-1 break-words text-sm text-charcoal-blue-500 dark:text-charcoal-blue-400">
                  {description}
                </DialogPrimitive.Description>
              ) : (
                <DialogPrimitive.Description className="sr-only">
                  Details
                </DialogPrimitive.Description>
              )}
            </div>
            <DialogPrimitive.Close
              className="btn-ghost btn-sm shrink-0"
              aria-label="Close"
            >
              <X className="h-4 w-4" aria-hidden="true" />
            </DialogPrimitive.Close>
          </div>
          {children}
        </DialogPrimitive.Content>
      </DialogPrimitive.Portal>
    </DialogPrimitive.Root>
  );
}

/** A label and its value, as a definition list row. */
export function DrawerField({
  label,
  children,
}: {
  label: string;
  children: React.ReactNode;
}) {
  return (
    <div className="grid grid-cols-[7rem_1fr] gap-3 border-b border-charcoal-blue-100 py-2 text-sm last:border-b-0 dark:border-white/10">
      <dt className="text-charcoal-blue-500 dark:text-charcoal-blue-400">
        {label}
      </dt>
      <dd className="min-w-0 break-words text-charcoal-blue-900 dark:text-charcoal-blue-50">
        {children}
      </dd>
    </div>
  );
}
