import React from "react";
import { HeaderNav } from "@/components/HeaderNav";
import { JobSearchForm } from "@/components/portal/JobSearchForm";
import { JobList } from "@/components/portal/JobList";
import { invokeBackend } from "@/lib/server/backend-invoke";
import type { Paged, PublicRequisitionDto } from "@/lib/types/requisition";

export const metadata = {
  title: "Browse Jobs | D4FAPE ATS",
  description: "Search and browse open roles at D4FAPE ATS",
};

type JobsPageSearchParams = { keyword?: string; page?: string };

// Public jobs list (AC-16–AC-20, AC-24). `page`/`keyword` are forwarded to
// `/api/public/requisitions` as-is; an invalid `page` is rejected by the backend with 400
// (AC-24) and surfaces via Next.js's default error boundary — no bespoke handling needed
// here (LLD §5.3).
export default async function JobsPage({
  searchParams,
}: {
  searchParams: Promise<JobsPageSearchParams>;
}) {
  const resolvedParams = await searchParams;
  const keyword = resolvedParams.keyword?.trim() || undefined;
  const page = resolvedParams.page || "1";

  const query = new URLSearchParams();
  if (keyword) {
    query.set("keyword", keyword);
  }
  query.set("page", page);

  const result = await invokeBackend<Paged<PublicRequisitionDto>>({
    path: `/api/public/requisitions?${query.toString()}`,
  });

  return (
    <main className="min-h-screen bg-slate-900 text-slate-100 p-6 md:p-12">
      <div className="max-w-4xl mx-auto space-y-8">
        <header className="flex flex-col md:flex-row items-center justify-between gap-4 pb-6 border-b border-slate-800">
          <div className="text-center md:text-left">
            <h1 className="text-2xl md:text-3xl font-extrabold tracking-tight text-white">
              Open Roles
            </h1>
            <p className="text-sm text-slate-400">Browse and search published requisitions</p>
          </div>
          <HeaderNav />
        </header>

        <JobSearchForm defaultKeyword={keyword} />

        <JobList
          items={result.items}
          page={result.page}
          pageSize={result.pageSize}
          total={result.total}
          keyword={keyword}
        />
      </div>
    </main>
  );
}
