import React from "react";
import Link from "next/link";
import type { CandidateApplicationListItemDto } from "@/lib/types/application";

export type ApplicationListProps = {
  items: CandidateApplicationListItemDto[];
};

// Presentational — candidate's own Applications (LLD §5.1, AC-12, AC-13). `cvDownloadUrl` is
// backend-relative (`/api/applications/{id}/cv`); every browser download link goes through
// `ui/bff`'s proxy, never straight to the backend origin.
export function ApplicationList({ items }: ApplicationListProps) {
  if (items.length === 0) {
    return (
      <div className="p-6 rounded-lg border border-slate-700 bg-slate-800/50 text-center text-slate-400">
        <p>
          You haven&apos;t applied to any roles yet.{" "}
          <Link href="/jobs" className="text-emerald-400 hover:text-emerald-300">
            Browse open roles
          </Link>
          .
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {items.map((application) => (
        <div
          key={application.id}
          className="p-5 rounded-lg border border-slate-700 bg-slate-800/70"
        >
          <div className="flex items-center justify-between gap-4">
            <h3 className="text-lg font-semibold text-slate-100">
              {application.requisitionTitle}
            </h3>
            <a
              href={`/api/bff/proxy${application.cvDownloadUrl}`}
              className="text-sm font-medium text-emerald-400 hover:text-emerald-300"
            >
              Download CV
            </a>
          </div>
          <p className="mt-1 text-xs uppercase tracking-wider text-slate-500">
            Submitted {new Date(application.submittedAtUtc).toLocaleDateString()}
          </p>
        </div>
      ))}
    </div>
  );
}
