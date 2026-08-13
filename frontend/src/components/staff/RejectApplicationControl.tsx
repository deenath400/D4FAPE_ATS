"use client";

import React, { useState } from "react";
import { useRouter } from "next/navigation";

const MAX_NOTE_LENGTH = 2000;

export type RejectApplicationControlProps = {
  applicationId: string;
};

// Note + confirm, rendered per Application card on the pipeline board (LLD §5.1, AC-14).
// Rejection is terminal (FR-10, FR-11), so a confirm step precedes the actual POST — mirrors
// `RequisitionLifecycleActions`'s "close" button being the destructive one in that family.
export function RejectApplicationControl({ applicationId }: RejectApplicationControlProps) {
  const router = useRouter();
  const [confirming, setConfirming] = useState(false);
  const [note, setNote] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleReject = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (note.length > MAX_NOTE_LENGTH) {
      setError("Note must be 2000 characters or fewer.");
      return;
    }

    setLoading(true);
    try {
      const res = await fetch(`/api/bff/proxy/applications/${applicationId}/reject`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ note: note.trim() || undefined }),
      });

      if (!res.ok) {
        const problem = await res.json().catch(() => null);
        setError(
          problem?.detail ||
            problem?.title ||
            "Unable to reject the application. Please try again.",
        );
        setLoading(false);
        return;
      }

      router.refresh();
      setLoading(false);
      setConfirming(false);
    } catch {
      setError("An unexpected network error occurred. Please try again.");
      setLoading(false);
    }
  };

  if (!confirming) {
    return (
      <button
        type="button"
        onClick={() => setConfirming(true)}
        className="w-full px-2 py-1.5 text-xs font-medium bg-red-800 hover:bg-red-700 text-white rounded"
      >
        Reject
      </button>
    );
  }

  return (
    <form onSubmit={handleReject} className="space-y-2">
      {error && (
        <div
          role="alert"
          className="p-2 bg-red-950/70 border border-red-800/80 rounded-lg text-red-200 text-xs leading-relaxed"
        >
          {error}
        </div>
      )}

      <p className="text-xs text-red-300">Reject this application? This cannot be undone.</p>

      <label htmlFor={`reject-note-${applicationId}`} className="sr-only">
        Note
      </label>
      <input
        id={`reject-note-${applicationId}`}
        type="text"
        value={note}
        onChange={(e) => setNote(e.target.value)}
        disabled={loading}
        placeholder="Optional note"
        className="w-full px-2 py-1.5 bg-slate-900 border border-slate-700 rounded text-slate-100 text-xs placeholder-slate-500 disabled:opacity-50"
      />

      <div className="flex gap-2">
        <button
          type="submit"
          disabled={loading}
          className="flex-1 px-2 py-1.5 text-xs font-medium bg-red-800 hover:bg-red-700 text-white rounded disabled:opacity-50"
        >
          {loading ? "Rejecting..." : "Confirm Reject"}
        </button>
        <button
          type="button"
          disabled={loading}
          onClick={() => setConfirming(false)}
          className="flex-1 px-2 py-1.5 text-xs font-medium bg-slate-700 hover:bg-slate-600 text-white rounded disabled:opacity-50"
        >
          Cancel
        </button>
      </div>
    </form>
  );
}
