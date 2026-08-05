import React from "react";
import { HeaderNav } from "@/components/HeaderNav";
import { invokeBackend } from "@/lib/server/backend-invoke";
import type { CandidateApplicationListItemDto } from "@/lib/types/application";
import { ApplicationList } from "@/components/portal/ApplicationList";

export const metadata = {
  title: "My Applications | D4FAPE ATS",
};

// Candidate data is per-session and always live — never statically prerendered at build time.
export const dynamic = "force-dynamic";

// "My Applications" (AC-12, AC-13). `/applications/*` is gated for the Candidate role by
// `middleware.ts` (T-28); this page trusts that gate, the same way `ui/staff`'s pages trust
// `/staff/*`'s gate.
export default async function MyApplicationsPage() {
  const applications = await invokeBackend<CandidateApplicationListItemDto[]>({
    path: "/api/applications/mine",
  });

  return (
    <main className="min-h-screen bg-slate-900 text-slate-100 p-6 md:p-12">
      <div className="max-w-3xl mx-auto space-y-8">
        <header className="flex flex-col md:flex-row items-center justify-between gap-4 pb-6 border-b border-slate-800">
          <h1 className="text-2xl md:text-3xl font-extrabold tracking-tight text-white">
            My Applications
          </h1>
          <HeaderNav />
        </header>

        <ApplicationList items={applications} />
      </div>
    </main>
  );
}
