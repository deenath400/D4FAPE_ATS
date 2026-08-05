import React from "react";

export type JobSearchFormProps = {
  defaultKeyword?: string;
};

// Progressive-enhancement GET form (LLD §5.1) — no client JS required. Submitting navigates
// to `/jobs?keyword=...`, read by the `(portal)/jobs/page.tsx` Server Component via
// `searchParams`. Omitting a `page` field on every search intentionally resets pagination to
// page 1 (AC-16, AC-20).
export function JobSearchForm({ defaultKeyword = "" }: JobSearchFormProps) {
  return (
    <form method="get" action="/jobs" role="search" className="flex gap-3">
      <label htmlFor="job-search-keyword" className="sr-only">
        Search jobs
      </label>
      <input
        id="job-search-keyword"
        type="search"
        name="keyword"
        defaultValue={defaultKeyword}
        placeholder="Search by title or description..."
        className="flex-1 px-3 py-2 bg-slate-900/90 border border-slate-700 rounded-lg text-slate-100 placeholder-slate-500 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:border-transparent transition-all text-sm"
      />
      <button
        type="submit"
        className="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white font-medium rounded-lg shadow-md transition-all text-sm"
      >
        Search
      </button>
    </form>
  );
}
