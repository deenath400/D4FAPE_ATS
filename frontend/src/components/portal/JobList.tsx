import React from "react";
import Link from "next/link";
import type { PublicRequisitionDto } from "@/lib/types/requisition";

export type JobListProps = {
  items: PublicRequisitionDto[];
  page: number;
  pageSize: number;
  total: number;
  keyword?: string;
};

function buildHref(page: number, keyword?: string): string {
  const params = new URLSearchParams();
  if (keyword) {
    params.set("keyword", keyword);
  }
  params.set("page", String(page));
  return `/jobs?${params.toString()}`;
}

// Presentational list + pagination (LLD §5.1). Prev/Next links are built from `searchParams`
// and disabled at the first/last page instead of omitted, so the layout doesn't shift.
export function JobList({ items, page, pageSize, total, keyword }: JobListProps) {
  if (items.length === 0) {
    return (
      <div className="p-6 rounded-lg border border-slate-700 bg-slate-800/50 text-center text-slate-400">
        <p>No roles match your search.</p>
      </div>
    );
  }

  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  const hasPrev = page > 1;
  const hasNext = page < totalPages;
  const disabledLinkClass =
    "px-3 py-1.5 rounded-lg border border-slate-800 text-slate-600 cursor-not-allowed";
  const activeLinkClass =
    "px-3 py-1.5 rounded-lg border border-slate-700 hover:bg-slate-800 text-slate-200";

  return (
    <div className="space-y-6">
      <div className="space-y-4">
        {items.map((job) => (
          <Link
            key={job.id}
            href={`/jobs/${job.id}`}
            className="block p-5 rounded-lg border border-slate-700 bg-slate-800/70 hover:border-emerald-600 transition-all"
          >
            <h3 className="text-lg font-semibold text-slate-100">{job.title}</h3>
            <p className="mt-2 text-sm text-slate-400 line-clamp-2">{job.description}</p>
          </Link>
        ))}
      </div>

      <div className="flex items-center justify-between text-sm text-slate-400">
        <span>
          Page {page} of {totalPages} &bull; {total} role{total === 1 ? "" : "s"}
        </span>
        <div className="flex gap-2">
          {hasPrev ? (
            <Link href={buildHref(page - 1, keyword)} className={activeLinkClass}>
              Previous
            </Link>
          ) : (
            <span className={disabledLinkClass}>Previous</span>
          )}
          {hasNext ? (
            <Link href={buildHref(page + 1, keyword)} className={activeLinkClass}>
              Next
            </Link>
          ) : (
            <span className={disabledLinkClass}>Next</span>
          )}
        </div>
      </div>
    </div>
  );
}
