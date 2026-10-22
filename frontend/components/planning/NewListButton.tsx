"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { createShoppingList } from "@/lib/api/shoppingLists";
import { appToast } from "@/lib/toast";

/** Starts an empty list and opens it. A list from a plan is made on the plan itself. */
export default function NewListButton() {
  const router = useRouter();
  const [busy, setBusy] = useState(false);
  return (
    <button
      type="button"
      className="btn-primary min-h-11 w-full sm:w-auto"
      disabled={busy}
      onClick={async () => {
        setBusy(true);
        try {
          const id = await createShoppingList({
            name: `Shopping ${new Date().toLocaleDateString(undefined, { day: "numeric", month: "short" })}`,
          });
          router.push(`/shopping-lists/${id}`);
        } catch (error) {
          appToast.error(error, "Could not create the list");
          setBusy(false);
        }
      }}
    >
      New list
    </button>
  );
}
