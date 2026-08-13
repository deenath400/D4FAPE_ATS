"use client";

import React, { useState } from "react";
import { useRouter } from "next/navigation";
import type { StageDto } from "@/lib/types/pipeline";

const MAX_NOTE_LENGTH = 2000;

export type MoveApplicationControlProps = {
  applicationId: string;
  currentStageId: string;
  stages: StageDto[];
};

// Target-Stage select + optional note + submit, rendered per Application card on the pipeline
// board (LLD §5.1, AC-11). On a `409 application.move.conflict` (AC-29, E-2), shows the
// Application's actual current Stage from the ProblemDetails extensions and refreshes so the
// board reflects reality rather than silently overwriting another Recruiter's move.
export function MoveApplicationControl({
  applicationId,
  currentStageId,
  stages,
}: MoveApplicationControlProps) {
  const router = useRouter();
  const ordered = [...stages].sort((a, b) => a.sortOrder - b.sortOrder);
  const defaultTarget = ordered.find((s) => s.id !== currentStageId)?.id ?? currentStageId;

  const [targetStageId, setTargetStageId] = useState(defaultTarget);
  const [note, setNote] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleMove = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (note.length > MAX_NOTE_LENGTH) {
      setError("Note must be 2000 characters or fewer.");
      return;
    }

    setLoading(true);
    try {
      const res = await fetch(`/api/bff/proxy/applications/${applicationId}/move`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          targetStageId,
          expectedCurrentStageId: currentStageId,
          note: note.trim() || undefined,
        }),
      });

      if (!res.ok) {
        const problem = await res.json().catch(() => null);
        if (res.status === 409 && problem?.code === "application.move.conflict") {
          setError(
            `This application already moved to ${problem.actualCurrentStageName ?? "another stage"}. Refreshing.`,
          );
          router.refresh();
          setLoading(false);
          return;
        }
        setError(
          problem?.detail || problem?.title || "Unable to move the application. Please try again.",
        );
        setLoading(false);
        return;
      }

      setNote("");
      router.refresh();
      setLoading(false);
    } catch {
      setError("An unexpected network error occurred. Please try again.");
      setLoading(false);
    }
  };

  return (
    <form onSubmit={handleMove} className="space-y-2">
      {error && (
        <div
          role="alert"
          className="p-2 bg-red-950/70 border border-red-800/80 rounded-lg text-red-200 text-xs leading-relaxed"
        >
          {error}
        </div>
      )}

      <label htmlFor={`move-target-${applicationId}`} className="sr-only">
        Move to stage
      </label>
      <select
        id={`move-target-${applicationId}`}
        value={targetStageId}
        onChange={(e) => setTargetStageId(e.target.value)}
        disabled={loading}
        className="w-full px-2 py-1.5 bg-slate-900 border border-slate-700 rounded text-slate-100 text-xs disabled:opacity-50"
      >
        {ordered.map((stage) => (
          <option key={stage.id} value={stage.id}>
            {stage.name}
          </option>
        ))}
      </select>

      <label htmlFor={`move-note-${applicationId}`} className="sr-only">
        Note
      </label>
      <input
        id={`move-note-${applicationId}`}
        type="text"
        value={note}
        onChange={(e) => setNote(e.target.value)}
        disabled={loading}
        placeholder="Optional note"
        className="w-full px-2 py-1.5 bg-slate-900 border border-slate-700 rounded text-slate-100 text-xs placeholder-slate-500 disabled:opacity-50"
      />

      <button
        type="submit"
        disabled={loading}
        className="w-full px-2 py-1.5 text-xs font-medium bg-emerald-600 hover:bg-emerald-500 text-white rounded disabled:opacity-50"
      >
        {loading ? "Moving..." : "Move"}
      </button>
    </form>
  );
}
