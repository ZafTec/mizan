"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import {
  Drawer,
  DrawerBody,
  DrawerCloseButton,
  DrawerContent,
  DrawerDescription,
  DrawerHeader,
  DrawerTitle,
} from "@/components/ui/drawer";
import { useNarrowScreen } from "@/lib/hooks/useMediaQuery";

/**
 * A drawer for one row of an admin list, opened by `?open={id}` so the row is
 * a link and the URL can be shared. Closing goes back to the list without the
 * parameter once the drawer has moved away. A sheet from the bottom of a
 * phone, a column from the right of a wider screen.
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
  const narrow = useNarrowScreen();
  const [open, setOpen] = useState(true);

  return (
    <Drawer
      open={open}
      onOpenChange={setOpen}
      onAnimationEnd={(isOpen) => {
        if (!isOpen) router.replace(closeHref, { scroll: false });
      }}
      direction={narrow ? "bottom" : "right"}
    >
      <DrawerContent className="sm:w-full sm:max-w-lg">
        <DrawerHeader>
          <div className="min-w-0 flex-1 pt-2">
            <DrawerTitle className="text-lg break-words">{title}</DrawerTitle>
            <DrawerDescription
              className={description ? "break-words" : "sr-only"}
            >
              {description ?? "Details"}
            </DrawerDescription>
          </div>
          <DrawerCloseButton />
        </DrawerHeader>
        <DrawerBody>{children}</DrawerBody>
      </DrawerContent>
    </Drawer>
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
