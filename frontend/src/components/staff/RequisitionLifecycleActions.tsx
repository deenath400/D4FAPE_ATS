"use client";

import React, { useState } from "react";
import { useRouter } from "next/navigation";
import type { RequisitionStatus } from "@/lib/types/requisition";

export type RequisitionLifecycleActionsProps = {
  requisitionId: string;
  status: RequisitionStatus;
  canWrite: boolean;
};

type LifecycleAction = "publish" | "unpublish" | "close";

const ACTION_LABEL: Record<LifecycleAction, string> = {
  publish: "Publish",
  unpublish: "Unpublish",
  close: "Close",
};

const ACTION_LOADING_LABEL: Record<LifecycleAction, string> = {
  publish: "Publishing...",
  unpublish: "Unpublishing...",
  close: "Closing...",
};

// Publish/unpublish/close buttons (LLD §5.1). `canWrite=false` (HiringManager, per
// clarification C-2) renders nothing — read-only staff never see write affordances at all.
export function RequisitionLifecycleActions({
  requisitionId,
  status,
  canWrite,
}: RequisitionLifecycleActionsProps) {
  const router = useRouter();
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!canWrite) {
    return null;
  }

  const runAction = async (action: LifecycleAction) => {
    setError(null);
    setLoading(true);

    try {
      const res = await fetch(`/api/bff/proxy/requisitions/${requisitionId}/${action}`, {
        method: "POST",
      });

      if (!res.ok) {
        const problem = await res.json().catch(() => null);
        setError(
          problem?.detail ||
            problem?.title ||
            "Unable to update the requisition status. Please try again.",
        );
        setLoading(false);
        return;
      }

      router.refresh();
      setLoading(false);
    } catch {
      setError("An unexpected network error occurred. Please try again.");
      setLoading(false);
    }
  };

  const buttonClass =
    "px-3.5 py-1.5 font-medium bg-slate-700 hover:bg-slate-600 text-white rounded-lg shadow-md transition-all text-sm disabled:opacity-50";

  return (
    <div className="space-y-3">
      {error && (
        <div
          role="alert"
          className="p-4 bg-red-950/70 border border-red-800/80 rounded-xl text-red-200 text-sm leading-relaxed"
        >
          {error}
        </div>
      )}

      <div className="flex gap-3">
        {status === "draft" && (
          <button
            type="button"
            disabled={loading}
            onClick={() => runAction("publish")}
            className={`${buttonClass} bg-emerald-600 hover:bg-emerald-500`}
          >
            {loading ? ACTION_LOADING_LABEL.publish : ACTION_LABEL.publish}
          </button>
        )}

        {status === "published" && (
          <>
            <button
              type="button"
              disabled={loading}
              onClick={() => runAction("unpublish")}
              className={buttonClass}
            >
              {loading ? ACTION_LOADING_LABEL.unpublish : ACTION_LABEL.unpublish}
            </button>
            <button
              type="button"
              disabled={loading}
              onClick={() => runAction("close")}
              className={`${buttonClass} bg-red-800 hover:bg-red-700`}
            >
              {loading ? ACTION_LOADING_LABEL.close : ACTION_LABEL.close}
            </button>
          </>
        )}

        {status === "closed" && <p className="text-sm text-slate-400">This requisition is closed.</p>}
      </div>
    </div>
  );
}
