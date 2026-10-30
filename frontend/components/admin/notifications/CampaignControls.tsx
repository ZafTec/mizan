"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { ConfirmDialog } from "@/components/ConfirmDialog";
import {
  Dialog,
  DialogBody,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  activateCampaign,
  changeCampaign,
  previewCampaignAudience,
  sendCampaignTest,
  type CampaignAudiencePreview,
  type CampaignStep,
  type NotificationCampaign,
} from "@/lib/api/admin-notifications";
import { appToast, getErrorMessage } from "@/lib/toast";

/**
 * Test, start, pause, resume and cancel. Starting asks the API for the
 * audience first and names the count in the confirmation; the start sends
 * that count back, so a changed audience is refused rather than sent to.
 */
export default function CampaignControls({
  campaign,
}: {
  campaign: NotificationCampaign;
}) {
  const router = useRouter();
  const [busy, setBusy] = useState(false);
  const [confirm, setConfirm] = useState<CampaignAudiencePreview | null>(null);
  const [cancelling, setCancelling] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const status = campaign.status;

  const run = async (work: () => Promise<void>, failure: string) => {
    setBusy(true);
    setError(null);
    try {
      await work();
    } catch (e) {
      setError(getErrorMessage(e, failure));
    } finally {
      setBusy(false);
    }
  };

  const test = () =>
    run(async () => {
      const result = await sendCampaignTest(campaign.id);
      const skipped = result.skipped.map((s) => s.reason).join(" ");
      if (result.queued.length > 0)
        appToast.success(
          `Test sent to you on ${result.queued.length} ${result.queued.length === 1 ? "channel" : "channels"}`,
        );
      if (skipped) setError(skipped);
    }, "Could not send the test");

  const review = () =>
    run(async () => {
      setConfirm(
        await previewCampaignAudience({
          purpose: campaign.purpose,
          audience: campaign.audience,
          channels: campaign.channels,
        }),
      );
    }, "Could not count the audience");

  const start = () =>
    run(async () => {
      if (!confirm) return;
      await activateCampaign(campaign.id, confirm.reachable);
      appToast.success("Started");
      setConfirm(null);
      router.refresh();
    }, "Could not start it");

  const step = (next: CampaignStep, done: string) =>
    run(async () => {
      await changeCampaign(campaign.id, next);
      appToast.success(done);
      setCancelling(false);
      router.refresh();
    }, "Could not change it");

  const isEvent = campaign.timing.send === "event";

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap gap-2">
        {status !== "cancelled" && (
          <button
            type="button"
            className="btn-secondary"
            disabled={busy}
            onClick={test}
          >
            Send a test to me
          </button>
        )}
        {status === "draft" && (
          <button
            type="button"
            className="btn-primary"
            disabled={busy}
            onClick={review}
          >
            Start…
          </button>
        )}
        {status === "active" && (
          <button
            type="button"
            className="btn-secondary"
            disabled={busy}
            onClick={() => step("pause", "Paused")}
          >
            Pause
          </button>
        )}
        {status === "paused" && (
          <button
            type="button"
            className="btn-primary"
            disabled={busy}
            onClick={() => step("resume", "Resumed")}
          >
            Resume
          </button>
        )}
        {status !== "cancelled" && (
          <button
            type="button"
            className="btn-ghost-destructive"
            disabled={busy}
            onClick={() => setCancelling(true)}
          >
            Cancel campaign
          </button>
        )}
      </div>
      {error && (
        <p
          role="alert"
          className="text-sm text-burnt-peach-700 dark:text-burnt-peach-400"
        >
          {error}
        </p>
      )}

      <Dialog
        open={confirm !== null}
        onOpenChange={(open) => !open && setConfirm(null)}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Start “{campaign.name}”?</DialogTitle>
            <DialogDescription>
              {isEvent
                ? "It runs for everyone the event reaches from now on."
                : campaign.summary}
            </DialogDescription>
          </DialogHeader>
          {confirm && (
            <>
              <DialogBody className="space-y-4">
                <p
                  role="status"
                  className="border border-charcoal-blue-200 p-3 text-sm dark:border-charcoal-blue-700"
                >
                  {isEvent ? "The audience reaches " : "It goes to "}
                  <strong className="tabular-nums" data-testid="confirm-count">
                    {confirm.reachable}{" "}
                    {confirm.reachable === 1 ? "person" : "people"}
                  </strong>{" "}
                  now ({confirm.label}).
                </p>
                {!isEvent && confirm.reachable === 0 && (
                  <p className="text-sm text-burnt-peach-700 dark:text-burnt-peach-400">
                    Nobody in that audience can receive it.
                  </p>
                )}
                {error && (
                  <p
                    role="alert"
                    className="text-sm text-burnt-peach-700 dark:text-burnt-peach-400"
                  >
                    {error}
                  </p>
                )}
              </DialogBody>
              <DialogFooter>
                <button
                  type="button"
                  className="btn-ghost"
                  disabled={busy}
                  onClick={() => setConfirm(null)}
                >
                  Back
                </button>
                <button
                  type="button"
                  className="btn-primary"
                  disabled={busy || (!isEvent && confirm.reachable === 0)}
                  onClick={start}
                >
                  {busy ? "Starting…" : `Start for ${confirm.reachable}`}
                </button>
              </DialogFooter>
            </>
          )}
        </DialogContent>
      </Dialog>

      <ConfirmDialog
        open={cancelling}
        onClose={() => setCancelling(false)}
        title={`Cancel “${campaign.name}”?`}
        description="Nothing queued goes out, and it cannot be started again."
        confirmLabel="Cancel campaign"
        pendingLabel="Cancelling…"
        cancelLabel="Keep it"
        errorFallback="Could not change it"
        onConfirm={async () => {
          await changeCampaign(campaign.id, "cancel");
          appToast.success("Cancelled");
          router.refresh();
        }}
      />
    </div>
  );
}
