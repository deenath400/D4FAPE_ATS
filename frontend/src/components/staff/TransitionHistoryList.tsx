import React from "react";
import type { StageTransitionDto } from "@/lib/types/pipeline";

export type TransitionHistoryListProps = {
  items: StageTransitionDto[];
};

// Chronological list for one Application: kind, from -> to (or "Rejected"), actor, note,
// timestamp (LLD §5.1, AC-20, AC-21, AC-30). Presentational — items already arrive in
// chronological order from `GET /api/applications/{id}/transitions`.
export function TransitionHistoryList({ items }: TransitionHistoryListProps) {
  if (items.length === 0) {
    return (
      <div className="p-6 rounded-lg border border-slate-700 bg-slate-800/50 text-center text-slate-400">
        <p>No transitions yet.</p>
      </div>
    );
  }

  return (
    <ul className="space-y-3">
      {items.map((transition) => (
        <li key={transition.id} className="p-4 rounded-lg border border-slate-700 bg-slate-900">
          <div className="flex items-center justify-between gap-4">
            <p className="text-sm font-medium text-slate-100">
              {transition.kind === "reject"
                ? `Rejected from ${transition.fromStageName}`
                : `${transition.fromStageName} → ${transition.toStageName}`}
            </p>
            <time className="text-xs text-slate-500" dateTime={transition.occurredAtUtc}>
              {new Date(transition.occurredAtUtc).toLocaleString()}
            </time>
          </div>
          <p className="mt-1 text-xs text-slate-400">By {transition.actorDisplayLabel}</p>
          {transition.note && <p className="mt-2 text-sm text-slate-300">{transition.note}</p>}
        </li>
      ))}
    </ul>
  );
}
