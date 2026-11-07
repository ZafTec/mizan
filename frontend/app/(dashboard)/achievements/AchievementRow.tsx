"use client";

import Image from "next/image";
import { useState } from "react";
import { ModalShell } from "@/components/ModalShell";
import type { AchievementDto } from "@/types/api-contracts";

function earnedOn(value: string | null | undefined) {
  return value
    ? new Date(value).toLocaleDateString(undefined, {
        day: "numeric",
        month: "short",
        year: "numeric",
      })
    : null;
}

/**
 * The default badges ship at 128 and 512 pixels under /badges; the record
 * names the small one. An uploaded badge has one size, so it is shown as is.
 */
function largeBadge(iconUrl: string) {
  return iconUrl.replace("/badges/128/", "/badges/512/");
}

function RowContent({
  achievement,
  detail,
  earned,
}: {
  achievement: AchievementDto;
  detail: (string | null | undefined)[];
  earned: boolean;
}) {
  return (
    <>
      {achievement.iconUrl && (
        <Image
          src={achievement.iconUrl}
          alt=""
          width={28}
          height={28}
          unoptimized
          className={`h-7 w-7 shrink-0 ${earned ? "" : "opacity-50 grayscale"}`}
        />
      )}
      <span className="min-w-0 flex-1">
        <span className="block font-medium">
          {achievement.name || "Achievement"}
        </span>
        <span className="tabular-nums mt-0.5 block text-sm text-muted-foreground">
          {detail.filter(Boolean).join(" · ")}
        </span>
      </span>
      <span className="tabular-nums whitespace-nowrap text-sm text-muted-foreground">
        <span className="text-base font-semibold text-foreground">
          {achievement.points}
        </span>{" "}
        pts
      </span>
    </>
  );
}

/** A row still to earn: what it takes and how far along it is. */
export function LockedAchievementRow({
  achievement,
  detail,
}: {
  achievement: AchievementDto;
  detail: (string | null | undefined)[];
}) {
  return (
    <li className="ledger-row">
      <RowContent achievement={achievement} detail={detail} earned={false} />
    </li>
  );
}

/** An earned row opens the badge at full size with what it was for. */
export function EarnedAchievementRow({
  achievement,
}: {
  achievement: AchievementDto;
}) {
  const [open, setOpen] = useState(false);
  const date = earnedOn(achievement.earnedAt);
  const name = achievement.name || "Achievement";

  return (
    <li>
      <button
        type="button"
        className="ledger-row w-full text-left transition-colors hover:bg-hair focus-visible:bg-hair"
        aria-haspopup="dialog"
        onClick={() => setOpen(true)}
      >
        <RowContent
          achievement={achievement}
          detail={[achievement.category, date ? `Earned ${date}` : null]}
          earned
        />
      </button>
      <ModalShell
        open={open}
        onClose={() => setOpen(false)}
        title={name}
        size="md"
      >
        <div className="flex flex-col items-center gap-5 pb-2 text-center">
          {achievement.iconUrl && (
            <Image
              src={largeBadge(achievement.iconUrl)}
              alt={`${name} badge`}
              width={512}
              height={512}
              unoptimized
              data-testid="achievement-badge"
              className="animate-badge-in aspect-square w-full max-w-[min(18rem,60dvh)] object-contain"
            />
          )}
          {achievement.description && (
            <p className="text-charcoal-blue-700 dark:text-charcoal-blue-300">
              {achievement.description}
            </p>
          )}
          <dl className="grid w-full grid-cols-3 gap-3 border-t border-border pt-4 text-sm">
            <div>
              <dt className="text-muted-foreground">Earned</dt>
              <dd className="tabular-nums mt-1 font-medium">{date ?? "—"}</dd>
            </div>
            <div>
              <dt className="text-muted-foreground">Points</dt>
              <dd className="tabular-nums mt-1 font-medium">
                {achievement.points}
              </dd>
            </div>
            <div>
              <dt className="text-muted-foreground">Category</dt>
              <dd className="mt-1 font-medium break-words">
                {achievement.category || "—"}
              </dd>
            </div>
          </dl>
        </div>
      </ModalShell>
    </li>
  );
}
