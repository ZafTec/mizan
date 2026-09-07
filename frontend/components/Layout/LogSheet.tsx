"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import * as Dialog from "@radix-ui/react-dialog";
import {
  ArrowLeft,
  ArrowRight,
  Dumbbell,
  Ruler,
  Utensils,
  X,
} from "lucide-react";
import FoodPhotoSheet from "@/components/ai/FoodPhotoSheet";
import MealLogForm from "@/components/logging/MealLogForm";
import MeasurementLogForm from "@/components/logging/MeasurementLogForm";
import { type LogKind } from "@/components/logging/LogEntryButton";
import type { RecipeResult } from "@/components/logging/logging";
import { appToast } from "@/lib/toast";
import { useSession } from "@/lib/auth-client";

type LogSheetProps = {
  open: boolean;
  onClose: () => void;
  initialKind?: LogKind;
  initialDate?: string;
  initialMealType?: string;
  initialRecipe?: RecipeResult;
};

/** Every entry starts over the current page and returns to that same context. */
export default function LogSheet(props: LogSheetProps) {
  // Mount a fresh draft on every opening; saved or dismissed values never leak
  // into the next meal. Radix owns focus trapping, Escape and scroll locking.
  return props.open ? <OpenLogSheet {...props} /> : null;
}

function OpenLogSheet({
  onClose,
  initialKind,
  initialDate,
  initialMealType,
  initialRecipe,
}: LogSheetProps) {
  const router = useRouter();
  const { data: session } = useSession();
  const timeZone = session?.user.timeZoneId || "UTC";
  const [kind, setKind] = useState<LogKind | "photo" | null>(
    initialKind ?? null,
  );
  const [busy, setBusy] = useState(false);
  const titleRef = useRef<HTMLHeadingElement>(null);
  const returnFocusRef = useRef<HTMLElement | null>(null);
  const title =
    kind === "meal"
      ? "Log a meal"
      : kind === "measurement"
        ? "Log a measurement"
        : kind === "workout"
          ? "Log a workout"
          : kind === "photo"
            ? "Log from a photo"
            : "What would you like to log?";
  const description =
    kind === "meal"
      ? "Find a food or recipe, adjust the amount, and add it to your day."
      : kind === "measurement"
        ? "A quick check-in. Add only the measurements you took."
        : kind === "workout"
          ? "Start a session or pick up where you left off."
          : kind === "photo"
            ? "Review the food and portions before saving."
            : "A meal, a training session, or a body measurement.";

  useEffect(() => {
    titleRef.current?.focus();
  }, [kind]);

  function saved(message: string) {
    appToast.success(message);
    router.refresh();
    onClose();
  }

  return (
    <Dialog.Root
      open
      onOpenChange={(next) => {
        if (!next && !busy) onClose();
      }}
    >
      <Dialog.Portal>
        <Dialog.Overlay className="fixed inset-0 z-[100] bg-charcoal-blue-950/45" />
        <Dialog.Content
          className={`fixed inset-x-0 bottom-0 z-[101] max-h-[92dvh] overscroll-contain rounded-t-md border border-border bg-background px-5 pt-5 text-foreground sm:left-1/2 sm:right-auto sm:top-1/2 sm:bottom-auto sm:w-[calc(100%-2rem)] sm:max-w-lg sm:-translate-x-1/2 sm:-translate-y-1/2 sm:rounded-md sm:px-6 sm:pt-6 ${kind === "meal" ? "flex h-[92dvh] flex-col overflow-hidden sm:h-auto" : "overflow-y-auto pb-[calc(1.25rem+env(safe-area-inset-bottom,0px))] sm:pb-6"}`}
          onOpenAutoFocus={(event) => {
            returnFocusRef.current =
              document.activeElement instanceof HTMLElement
                ? document.activeElement
                : null;
            event.preventDefault();
            titleRef.current?.focus();
          }}
          onCloseAutoFocus={(event) => {
            event.preventDefault();
            returnFocusRef.current?.focus();
          }}
          onEscapeKeyDown={(event) => {
            if (busy) event.preventDefault();
          }}
          onPointerDownOutside={(event) => {
            if (busy) event.preventDefault();
          }}
        >
          <header className="mb-6 shrink-0">
            <div className="mb-3 flex items-center justify-between gap-4">
              {kind ? (
                <button
                  type="button"
                  onClick={() => setKind(null)}
                  disabled={busy}
                  className="flex min-h-11 items-center gap-2 text-sm text-charcoal-blue-700 disabled:opacity-50 dark:text-charcoal-blue-300"
                >
                  <ArrowLeft aria-hidden="true" size={16} strokeWidth={1.7} />
                  All entries
                </button>
              ) : (
                <span />
              )}
              <Dialog.Close
                aria-label="Close logging"
                disabled={busy}
                className="-mr-2 flex h-11 w-11 items-center justify-center rounded-sm text-charcoal-blue-700 hover:bg-muted disabled:opacity-50 dark:text-charcoal-blue-300"
              >
                <X aria-hidden="true" size={20} strokeWidth={1.7} />
              </Dialog.Close>
            </div>
            <Dialog.Title
              ref={titleRef}
              tabIndex={-1}
              className="text-xl font-semibold tracking-tight outline-none"
            >
              {title}
            </Dialog.Title>
            <Dialog.Description className="mt-2 text-sm leading-relaxed text-charcoal-blue-700 dark:text-charcoal-blue-300">
              {description}
            </Dialog.Description>
          </header>
          {kind === null && (
            <div className="divide-y divide-border border-y border-border">
              {(
                [
                  {
                    kind: "meal",
                    title: "Meal",
                    detail: "Foods, recipes, and quick entries",
                    icon: Utensils,
                  },
                  {
                    kind: "workout",
                    title: "Workout",
                    detail: "Exercises, sets, and reps",
                    icon: Dumbbell,
                  },
                  {
                    kind: "measurement",
                    title: "Measurement",
                    detail: "Weight and body measurements",
                    icon: Ruler,
                  },
                ] as const
              ).map((item) => (
                <button
                  key={item.kind}
                  type="button"
                  onClick={() => setKind(item.kind)}
                  className="flex min-h-[88px] w-full items-center gap-4 px-1 py-4 text-left transition-colors hover:bg-muted"
                >
                  <item.icon
                    aria-hidden="true"
                    size={23}
                    strokeWidth={1.7}
                    className="shrink-0 text-brand-700 dark:text-brand-300"
                  />
                  <span className="min-w-0 flex-1">
                    <span className="block font-semibold">{item.title}</span>
                    <span className="mt-1 block text-sm text-charcoal-blue-700 dark:text-charcoal-blue-300">
                      {item.detail}
                    </span>
                  </span>
                  <ArrowRight aria-hidden="true" size={17} strokeWidth={1.7} />
                </button>
              ))}
            </div>
          )}
          {kind === "meal" && (
            <MealLogForm
              initialDate={initialDate}
              initialMealType={initialMealType}
              initialRecipe={initialRecipe}
              timeZone={timeZone}
              onSaved={saved}
              onBusyChange={setBusy}
              onUsePhoto={() => setKind("photo")}
            />
          )}
          {kind === "measurement" && (
            <MeasurementLogForm
              initialDate={initialDate}
              timeZone={timeZone}
              onSaved={saved}
              onBusyChange={setBusy}
            />
          )}
          {kind === "workout" && (
            <div className="space-y-5">
              <p className="text-sm leading-relaxed text-charcoal-blue-700 dark:text-charcoal-blue-300">
                Your workout has its own space so you can record sets as you
                train. An active session is saved as you go.
              </p>
              <Link
                href={
                  initialDate
                    ? `/workout/active?date=${encodeURIComponent(initialDate)}`
                    : "/workout/active"
                }
                onClick={onClose}
                className="btn-primary min-h-11 w-full"
              >
                Open workout
                <ArrowRight aria-hidden="true" size={17} strokeWidth={1.7} />
              </Link>
            </div>
          )}
          {kind === "photo" && (
            <FoodPhotoSheet
              initialDate={initialDate}
              initialMealType={initialMealType}
              onBusyChange={setBusy}
              onLogged={() => {
                router.refresh();
                onClose();
              }}
            />
          )}
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
}
